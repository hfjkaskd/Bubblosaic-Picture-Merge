using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace RemoteImageDelivery.Editor
{
    public sealed class RemoteImageDeliveryWindow : EditorWindow
    {
        enum DashboardTab
        {
            Configuration,
            Runtime,
            Cache,
            UrlDiagnostics,
        }

        enum HistoryStateFilter
        {
            All = -1,
            Succeeded = (int)RemoteImageRequestState.Succeeded,
            Failed = (int)RemoteImageRequestState.Failed,
        }

        struct CacheScan
        {
            public string Root;
            public string Error;
            public int DataFiles;
            public int MetadataFiles;
            public int PartialFiles;
            public long DataBytes;
            public long PartialBytes;
        }

        const string ConfigGuidPreference =
            "RemoteImageDelivery.Editor.ConfigGuid";
        const double RefreshIntervalSeconds = 0.25d;

        static readonly string[] EndpointProperties =
        {
            "remoteEnabled",
            "baseUrl",
            "contentRoot",
            "urlMode",
            "urlTemplate",
            "allowAbsoluteFallbackUrl",
            "resizeAbsoluteFallbackWithImageView",
            "acceptHeader",
        };

        static readonly string[] SchedulingProperties =
        {
            "itemsPerGroup",
            "highPriorityConcurrency",
            "lowPriorityConcurrency",
            "cacheDecodesPerFrame",
            "foregroundMaxAttempts",
            "backgroundMaxAttempts",
            "cancelNetworkRequestOnTimeout",
            "requestTimeoutSeconds",
            "retryBaseDelaySeconds",
            "retryJitter",
            "prefetchNextItemHigh",
            "prefetchCurrentAndNextGroup",
            "advancePrefetchWindow",
            "nextGroupThreshold",
            "pruneItemsBeforeCurrent",
        };

        static readonly string[] PlayerDeliveryGateProperties =
        {
            "bundledSeedItemCount",
            "preferRemoteAfterSeedItems",
            "foregroundBatchTimeoutSeconds",
        };

        static readonly string[] CacheProperties =
        {
            "cacheNamespace",
            "memoryTextureLimit",
            "enforceDiskCacheBudget",
            "maxDiskCacheMiB",
            "resumePartialDownloads",
            "resumeMinimumBytes",
            "validateImageHeader",
            "validateDecodedTexture",
            "verifySha256WhenProvided",
            "verifyByteCountWhenProvided",
        };

        static readonly string[] DiagnosticProperties =
        {
            "verboseLogging",
            "keepRequestHistory",
            "requestHistoryLimit",
        };

        RemoteImageDeliveryConfig _config;
        SerializedObject _serializedConfig;
        DashboardTab _tab;
        Vector2 _mainScroll;
        Vector2 _historyScroll;
        string _historySearch = "";
        HistoryStateFilter _historyState = HistoryStateFilter.All;
        int _historyDisplayLimit = 200;
        bool _historyNewestFirst = true;
        double _nextRefreshTime;
        RemoteImageDeliverySnapshot _snapshot;
        string _snapshotError = "";

        string _probeKey = "diagnostic-image";
        string _probeRelativePath = "";
        string _probeAbsoluteUrl = "";
        string _probeVariant = "512";
        string _probeSha256 = "";
        long _probeExpectedBytes;
        int _probeWidth = 512;
        int _probeHeight = 512;
        int _probeGroup = 1;
        int _probeItem = 1;
        UnityWebRequest _probeRequest;
        UnityWebRequestAsyncOperation _probeOperation;
        RemoteImageAsset _activeProbeAsset;
        Stopwatch _probeWatch;
        string _probeSummary = "";
        MessageType _probeMessageType = MessageType.Info;

        [MenuItem(
            "Tools/远程图片分发/监控面板",
            priority = 1)]
        public static void Open()
        {
            var window = GetWindow<RemoteImageDeliveryWindow>();
            window.titleContent =
                new GUIContent("远程图片分发");
            window.minSize = new Vector2(760f, 540f);
            window.Show();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("远程图片分发");
            minSize = new Vector2(760f, 540f);
            LoadRememberedConfiguration();
            EditorApplication.update += OnEditorUpdate;
            RefreshRuntimeSnapshot();
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DisposeProbe();
        }

        void OnEditorUpdate()
        {
            if (_probeOperation != null && _probeOperation.isDone)
                CompleteProbe();

            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime =
                EditorApplication.timeSinceStartup +
                RefreshIntervalSeconds;
            RefreshRuntimeSnapshot();

            if (_tab == DashboardTab.Runtime ||
                _tab == DashboardTab.Cache ||
                _probeOperation != null)
            {
                Repaint();
            }
        }

        void OnGUI()
        {
            DrawTopBar();
            _tab = (DashboardTab)GUILayout.Toolbar(
                (int)_tab,
                new[]
                {
                    "配置",
                    "运行队列与历史",
                    "缓存",
                    "URL 诊断",
                },
                GUILayout.Height(25f));

            EditorGUILayout.Space(4f);
            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            switch (_tab)
            {
                case DashboardTab.Configuration:
                    DrawConfiguration();
                    break;
                case DashboardTab.Runtime:
                    DrawRuntime();
                    break;
                case DashboardTab.Cache:
                    DrawCache();
                    break;
                case DashboardTab.UrlDiagnostics:
                    DrawUrlDiagnostics();
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawTopBar()
        {
            using (new EditorGUILayout.HorizontalScope(
                       EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var selected = (RemoteImageDeliveryConfig)
                    EditorGUILayout.ObjectField(
                        _config,
                        typeof(RemoteImageDeliveryConfig),
                        false,
                        GUILayout.MinWidth(260f));
                if (EditorGUI.EndChangeCheck())
                    SetConfiguration(selected);

                if (GUILayout.Button(
                        "创建默认配置",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(110f)))
                {
                    RemoteImageDeliveryConfig created =
                        RemoteImageDeliveryConfigAssetMenu
                            .CreateDefaultConfiguration();
                    if (created != null)
                        SetConfiguration(created);
                }

                RemoteImageDeliveryClient active =
                    RemoteImageDeliveryClient.Active;
                using (new EditorGUI.DisabledScope(
                           active == null ||
                           ReferenceEquals(active.Config, _config)))
                {
                    if (GUILayout.Button(
                            "使用运行时配置",
                            EditorStyles.toolbarButton,
                            GUILayout.Width(120f)))
                    {
                        SetConfiguration(active.Config);
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    active == null
                        ? "运行时：未连接"
                        : "运行时：已连接",
                    EditorStyles.miniLabel);
            }
        }

        void DrawConfiguration()
        {
            DrawSectionHeader(
                "配置资源",
                "一个可复用的 ScriptableObject 统一控制 URL 解析、下载调度、缓存策略与诊断。");

            if (_config == null)
            {
                EditorGUILayout.HelpBox(
                    "请选择现有的 RemoteImageDeliveryConfig，或创建默认配置。" +
                    "客户端配置中严禁保存上传密钥。",
                    MessageType.Info);
                if (GUILayout.Button(
                        "创建默认配置",
                        GUILayout.Height(30f)))
                {
                    RemoteImageDeliveryConfig created =
                        RemoteImageDeliveryConfigAssetMenu
                            .CreateDefaultConfiguration();
                    if (created != null)
                        SetConfiguration(created);
                }
                return;
            }

            EnsureSerializedConfiguration();
            _serializedConfig.Update();

            DrawPropertySection(
                "CDN 端点",
                "配置公开访问地址以及资源路径的拼接方式。",
                EndpointProperties);
            DrawResolvedEndpointSummary();
            DrawPropertySection(
                "玩家侧关键分发开关",
                "这三个参数决定首批关卡来自哪里，以及进入关卡时整批图片最多等待多久。",
                PlayerDeliveryGateProperties);
            EditorGUILayout.HelpBox(
                "BubblePics 兼容默认值：安装包内置第 1–10 关；第 10 关之后允许优先使用远程图片；" +
                "前台整批加载最长等待 60 秒。即使开发目录仍保留本地图片，也可以开启“种子关卡后优先远程”来测试真实 CDN。",
                MessageType.Info);
            DrawPropertySection(
                "下载调度",
                "与原版兼容的高低优先级队列以及分批预取节奏。",
                SchedulingProperties);
            DrawPropertySection(
                "缓存",
                "持久化缓存、内存缓存容量以及内容校验策略。",
                CacheProperties);
            DrawPropertySection(
                "诊断",
                "运行时日志以及请求历史记录设置。",
                DiagnosticProperties);

            if (_serializedConfig.ApplyModifiedProperties())
            {
                _config.Sanitize();
                EditorUtility.SetDirty(_config);
                RefreshRuntimeSnapshot();
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("规范化并保存"))
                {
                    Undo.RecordObject(
                        _config,
                        "规范化远程图片分发配置");
                    _config.Sanitize();
                    EditorUtility.SetDirty(_config);
                    AssetDatabase.SaveAssets();
                    _serializedConfig.Update();
                }

                if (GUILayout.Button("在项目中定位配置"))
                {
                    Selection.activeObject = _config;
                    EditorGUIUtility.PingObject(_config);
                }
            }
        }

        void DrawResolvedEndpointSummary()
        {
            RemoteImageAsset sample = BuildProbeAsset();
            string resolved = _config.ResolveUrl(sample);
            bool configured =
                _config.IsConfiguredForRemote(out string reason);

            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "配置状态",
                    configured ? "就绪" : reason);
                EditorGUILayout.LabelField(
                    "示例解析 URL",
                    string.IsNullOrEmpty(resolved)
                        ? "（请在“URL 诊断”中填写相对路径）"
                        : resolved,
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        void DrawRuntime()
        {
            DrawSectionHeader(
                "实时运行状态",
                "此面板读取 RemoteImageDeliveryClient.Active，" +
                "不会自动创建隐藏的运行时客户端。");

            RemoteImageDeliveryClient client =
                RemoteImageDeliveryClient.Active;
            if (client == null)
            {
                EditorGUILayout.HelpBox(
                    "当前没有活动客户端。请进入运行模式，并通过已配置的宿主组件" +
                    "初始化 RemoteImageDeliveryClient，之后即可查看实时队列和请求历史。",
                    MessageType.Info);
                return;
            }

            if (!string.IsNullOrEmpty(_snapshotError))
            {
                EditorGUILayout.HelpBox(
                    _snapshotError,
                    MessageType.Warning);
            }

            RemoteImageDeliverySnapshot snapshot =
                _snapshot ?? client.GetSnapshot();
            DrawQueueSummary(snapshot, client.Config);
            DrawSessionSummary(snapshot);
            DrawHistory(client.History);
        }

        void DrawQueueSummary(
            RemoteImageDeliverySnapshot snapshot,
            RemoteImageDeliveryConfig runtimeConfig)
        {
            DrawSectionHeader(
                "下载队列",
                "相同稳定资源键的重复请求会自动合并。");
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetric(
                    "高优先级",
                    $"{snapshot.ActiveHigh}/{runtimeConfig.highPriorityConcurrency}",
                    snapshot.QueuedHigh + " 个排队中");
                DrawMetric(
                    "低优先级",
                    $"{snapshot.ActiveLow}/{runtimeConfig.lowPriorityConcurrency}",
                    snapshot.QueuedLow + " 个排队中");
                DrawMetric(
                    "独立请求",
                    snapshot.InFlightUnique.ToString(),
                    "个正在处理");
                DrawMetric(
                    "等待总数",
                    (snapshot.QueuedHigh + snapshot.QueuedLow).ToString(),
                    "包含全部优先级");
            }
        }

        void DrawSessionSummary(RemoteImageDeliverySnapshot snapshot)
        {
            DrawSectionHeader(
                "本次会话",
                "创建新的客户端时会重置这些计数。");
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetric(
                    "已下载",
                    FormatBytes(snapshot.SessionDownloadedBytes),
                    snapshot.SessionNetworkSuccesses + " 次网络成功");
                DrawMetric(
                    "缓存命中",
                    (snapshot.SessionMemoryHits +
                     snapshot.SessionDiskHits).ToString(),
                    $"{snapshot.SessionMemoryHits} 次内存 / " +
                    $"{snapshot.SessionDiskHits} 次磁盘");
                DrawMetric(
                    "失败",
                    snapshot.SessionFailures.ToString(),
                    "个已完成请求");
                DrawMetric(
                    "磁盘缓存",
                    FormatBytes(snapshot.CacheBytes),
                    snapshot.CacheFiles + " 张图片");
            }
        }

        void DrawHistory(IReadOnlyList<RemoteImageRequestRecord> history)
        {
            DrawSectionHeader(
                "请求历史",
                "显示已完成的内存、磁盘、网络请求和失败请求。");

            using (new EditorGUILayout.HorizontalScope())
            {
                _historySearch = EditorGUILayout.TextField(
                    "搜索",
                    _historySearch);
                int stateFilter = _historyState == HistoryStateFilter.Succeeded
                    ? 1
                    : _historyState == HistoryStateFilter.Failed
                        ? 2
                        : 0;
                stateFilter = EditorGUILayout.Popup(
                    stateFilter,
                    new[] { "全部", "成功", "失败" },
                    GUILayout.Width(110f));
                _historyState = stateFilter == 1
                    ? HistoryStateFilter.Succeeded
                    : stateFilter == 2
                        ? HistoryStateFilter.Failed
                        : HistoryStateFilter.All;
                _historyNewestFirst = GUILayout.Toggle(
                    _historyNewestFirst,
                    "最新优先",
                    GUILayout.Width(95f));
                GUILayout.Label("上限", GUILayout.Width(32f));
                _historyDisplayLimit = EditorGUILayout.IntSlider(
                    _historyDisplayLimit,
                    20,
                    1000,
                    GUILayout.Width(205f));
            }

            using (new EditorGUILayout.HorizontalScope(
                       EditorStyles.toolbar))
            {
                HistoryHeader("状态", 72f);
                HistoryHeader("优先级", 58f);
                HistoryHeader("来源", 86f);
                HistoryHeader("组/项目", 76f);
                HistoryHeader("字节数", 72f);
                HistoryHeader("耗时", 62f);
                HistoryHeader("资源键 / URL / 错误", 320f);
            }

            _historyScroll = EditorGUILayout.BeginScrollView(
                _historyScroll,
                false,
                true,
                GUILayout.MinHeight(180f),
                GUILayout.MaxHeight(420f));

            int shown = 0;
            if (history != null)
            {
                int start = _historyNewestFirst
                    ? history.Count - 1
                    : 0;
                int end = _historyNewestFirst
                    ? -1
                    : history.Count;
                int step = _historyNewestFirst ? -1 : 1;

                for (int i = start;
                     i != end && shown < _historyDisplayLimit;
                     i += step)
                {
                    RemoteImageRequestRecord record = history[i];
                    if (!MatchesHistoryFilter(record))
                        continue;
                    DrawHistoryRow(record, shown);
                    shown++;
                }
            }
            EditorGUILayout.EndScrollView();

            if (shown == 0)
            {
                EditorGUILayout.HelpBox(
                    history == null || history.Count == 0
                        ? "尚未记录任何已完成请求。"
                        : "没有符合当前筛选条件的请求。",
                    MessageType.None);
            }
        }

        void DrawHistoryRow(
            RemoteImageRequestRecord record,
            int visibleIndex)
        {
            Color previous = GUI.backgroundColor;
            if (record.state == RemoteImageRequestState.Failed)
                GUI.backgroundColor = new Color(1f, 0.72f, 0.72f);
            else if ((visibleIndex & 1) == 1)
                GUI.backgroundColor = new Color(0.92f, 0.92f, 0.92f);

            using (new EditorGUILayout.HorizontalScope(
                       EditorStyles.helpBox))
            {
                GUILayout.Label(
                    LocalizeState(record.state),
                    GUILayout.Width(72f));
                GUILayout.Label(
                    LocalizePriority(record.priority),
                    GUILayout.Width(58f));
                GUILayout.Label(
                    LocalizeSource(record.source),
                    GUILayout.Width(86f));
                GUILayout.Label(
                    $"{record.groupIndex}/{record.itemIndex}",
                    GUILayout.Width(76f));
                GUILayout.Label(
                    FormatBytes(record.bytes),
                    GUILayout.Width(72f));
                GUILayout.Label(
                    record.durationMilliseconds + " ms",
                    GUILayout.Width(62f));

                string detail = FirstNonEmpty(
                    record.error,
                    record.url,
                    record.relativePath,
                    record.key);
                GUILayout.Label(
                    new GUIContent(detail, BuildHistoryTooltip(record)),
                    EditorStyles.wordWrappedMiniLabel,
                    GUILayout.MinWidth(320f));
            }
            GUI.backgroundColor = previous;
        }

        void DrawCache()
        {
            DrawSectionHeader(
                "持久化缓存",
                "所有清理操作都使用活动客户端验证过的缓存根目录，" +
                "并且执行前需要明确确认。");

            CacheScan scan = ScanCache();
            if (!string.IsNullOrEmpty(scan.Error))
            {
                EditorGUILayout.HelpBox(
                    scan.Error,
                    MessageType.Warning);
            }

            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "根目录",
                    string.IsNullOrEmpty(scan.Root)
                        ? "（需要先配置）"
                        : scan.Root,
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    "已提交图片",
                    $"{scan.DataFiles} 个文件 / {FormatBytes(scan.DataBytes)}");
                EditorGUILayout.LabelField(
                    "元数据",
                    scan.MetadataFiles + " 个文件");
                EditorGUILayout.LabelField(
                    "未完成下载",
                    $"{scan.PartialFiles} 个文件 / " +
                    FormatBytes(scan.PartialBytes));
                if (_config != null)
                {
                    long budget =
                        (long)_config.maxDiskCacheMiB * 1024L * 1024L;
                    float fraction = budget > 0
                        ? Mathf.Clamp01((float)scan.DataBytes / budget)
                        : 0f;
                    Rect rect = GUILayoutUtility.GetRect(
                        20f,
                        20f,
                        GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(
                        rect,
                        fraction,
                        $"{FormatBytes(scan.DataBytes)} / " +
                        $"{FormatBytes(budget)}");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           string.IsNullOrEmpty(scan.Root)))
                {
                    if (GUILayout.Button("打开缓存目录"))
                        OpenCacheFolder(scan.Root);
                }

                RemoteImageDeliveryClient client =
                    RemoteImageDeliveryClient.Active;
                using (new EditorGUI.DisabledScope(client == null))
                {
                    if (GUILayout.Button("按配置容量清理"))
                    {
                        int removed = client.PruneToBudget();
                        RefreshRuntimeSnapshot();
                        ShowNotification(
                            new GUIContent(
                                $"已清理 {removed} 张缓存图片。"));
                    }

                    if (GUILayout.Button("清空内存缓存"))
                    {
                        client.ClearMemoryCache();
                        RefreshRuntimeSnapshot();
                        ShowNotification(
                            new GUIContent("内存缓存已清空。"));
                    }
                }
            }

            RemoteImageDeliveryClient active =
                RemoteImageDeliveryClient.Active;
            using (new EditorGUI.DisabledScope(active == null))
            {
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.72f, 0.65f);
                if (GUILayout.Button(
                        "清空已提交磁盘缓存…",
                        GUILayout.Height(30f)))
                {
                    ConfirmAndClearCache(active, scan);
                }
                GUI.backgroundColor = previous;
            }

            if (active == null)
            {
                EditorGUILayout.HelpBox(
                    "没有活动运行时客户端时会禁用清理功能，以防编辑器误判需要删除的" +
                    "缓存目录。请进入运行模式或连接客户端，然后确认上方显示的准确路径。",
                    MessageType.Info);
            }
        }

        void DrawUrlDiagnostics()
        {
            DrawSectionHeader(
                "URL 预览",
                "按项目适配器的相同方式构造资源描述，然后查看并测试最终的分发 URL。");

            if (_config == null)
            {
                EditorGUILayout.HelpBox(
                    "请先指定配置资源，再解析 URL。",
                    MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                _probeKey = EditorGUILayout.TextField(
                    "稳定资源键",
                    _probeKey);
                _probeRelativePath = EditorGUILayout.TextField(
                    "相对路径",
                    _probeRelativePath);
                _probeAbsoluteUrl = EditorGUILayout.TextField(
                    "绝对地址回退 URL",
                    _probeAbsoluteUrl);
                _probeVariant = EditorGUILayout.TextField(
                    "变体",
                    _probeVariant);
                _probeSha256 = EditorGUILayout.TextField(
                    "预期 SHA-256",
                    _probeSha256);
                _probeExpectedBytes = EditorGUILayout.LongField(
                    "预期字节数",
                    _probeExpectedBytes);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _probeWidth = EditorGUILayout.IntField(
                        "宽度",
                        _probeWidth);
                    _probeHeight = EditorGUILayout.IntField(
                        "高度",
                        _probeHeight);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    _probeGroup = EditorGUILayout.IntField(
                        "组",
                        _probeGroup);
                    _probeItem = EditorGUILayout.IntField(
                        "项目",
                        _probeItem);
                }
            }

            RemoteImageAsset asset = BuildProbeAsset();
            string normalizedRelative =
                RemoteImageDeliveryConfig.NormalizeRelativePart(
                    asset.relativePath);
            string resolved = _config.ResolveUrl(asset);
            bool validHttpUrl =
                TryGetHttpUri(resolved, out Uri resolvedUri);

            if (!string.IsNullOrWhiteSpace(asset.relativePath) &&
                string.IsNullOrEmpty(normalizedRelative))
            {
                EditorGUILayout.HelpBox(
                    "相对路径不安全，解析结果为空。不允许使用父级目录跳转。",
                    MessageType.Error);
            }

            EditorGUILayout.LabelField("解析后的 URL");
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(resolved) ? "（空）" : resolved,
                EditorStyles.textArea,
                GUILayout.MinHeight(42f));

            if (validHttpUrl &&
                !string.Equals(
                    resolvedUri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox(
                    "解析后的端点不是 HTTPS。生产环境分发应使用 HTTPS。",
                    MessageType.Warning);
            }
            else if (!string.IsNullOrEmpty(resolved) && !validHttpUrl)
            {
                EditorGUILayout.HelpBox(
                    "解析结果不是有效的绝对 HTTP(S) URL。",
                    MessageType.Error);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           string.IsNullOrEmpty(resolved)))
                {
                    if (GUILayout.Button("复制 URL"))
                    {
                        EditorGUIUtility.systemCopyBuffer = resolved;
                        ShowNotification(new GUIContent("URL 已复制。"));
                    }
                }

                using (new EditorGUI.DisabledScope(!validHttpUrl))
                {
                    if (GUILayout.Button("在浏览器中打开"))
                        Application.OpenURL(resolved);
                }

                using (new EditorGUI.DisabledScope(
                           !validHttpUrl || _probeOperation != null))
                {
                    if (GUILayout.Button("下载并校验"))
                        StartProbe(resolved, asset);
                }

                using (new EditorGUI.DisabledScope(
                           _probeOperation == null))
                {
                    if (GUILayout.Button("取消"))
                        CancelProbe();
                }
            }

            if (_probeOperation != null)
            {
                float progress = Mathf.Clamp01(_probeOperation.progress);
                Rect rect = GUILayoutUtility.GetRect(
                    20f,
                    20f,
                    GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(
                    rect,
                    progress,
                    $"正在下载 {progress:P0}");
            }

            if (!string.IsNullOrEmpty(_probeSummary))
            {
                EditorGUILayout.HelpBox(
                    _probeSummary,
                    _probeMessageType);
            }

            EditorGUILayout.HelpBox(
                "诊断下载会使用配置的 Accept 请求头和超时时间，并校验 HTTP 状态、" +
                "预期字节数、可选的 SHA-256、图片文件头以及 Unity 纹理解码。" +
                "测试数据只保存在内存中，不会写入运行时缓存。",
                MessageType.None);
        }

        void DrawPropertySection(
            string title,
            string description,
            IEnumerable<string> propertyNames)
        {
            DrawSectionHeader(title, description);
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                foreach (string propertyName in propertyNames)
                {
                    SerializedProperty property =
                        _serializedConfig.FindProperty(propertyName);
                    if (property != null)
                        RemoteImageDeliveryEditorLabels.DrawProperty(property);
                }
            }
        }

        static void DrawSectionHeader(
            string title,
            string description)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(description))
            {
                EditorGUILayout.LabelField(
                    description,
                    EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.Space(2f);
        }

        static void DrawMetric(
            string title,
            string value,
            string note)
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.MinWidth(130f),
                       GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label(title, EditorStyles.miniBoldLabel);
                GUILayout.Label(value, EditorStyles.largeLabel);
                GUILayout.Label(note, EditorStyles.wordWrappedMiniLabel);
            }
        }

        static void HistoryHeader(string label, float width)
        {
            GUILayout.Label(
                label,
                EditorStyles.miniBoldLabel,
                GUILayout.Width(width));
        }

        bool MatchesHistoryFilter(RemoteImageRequestRecord record)
        {
            if (record == null) return false;
            if (_historyState != HistoryStateFilter.All &&
                (int)record.state != (int)_historyState)
            {
                return false;
            }

            string search = (_historySearch ?? "").Trim();
            if (search.Length == 0) return true;
            return Contains(record.key, search) ||
                   Contains(record.url, search) ||
                   Contains(record.relativePath, search) ||
                   Contains(record.localPath, search) ||
                   Contains(record.error, search) ||
                   Contains(record.source.ToString(), search) ||
                   Contains(record.priority.ToString(), search) ||
                   Contains(LocalizeState(record.state), search) ||
                   Contains(LocalizeSource(record.source), search) ||
                   Contains(LocalizePriority(record.priority), search);
        }

        static bool Contains(string value, string search)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(
                       search,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string BuildHistoryTooltip(
            RemoteImageRequestRecord record)
        {
            return
                $"资源键：{record.key}\n" +
                $"URL: {record.url}\n" +
                $"相对路径：{record.relativePath}\n" +
                $"本地路径：{record.localPath}\n" +
                $"开始时间（UTC）：{record.startedUtc}\n" +
                $"尝试次数：{record.attempts}\n" +
                $"错误：{record.error}";
        }

        static string LocalizeState(RemoteImageRequestState state)
        {
            switch (state)
            {
                case RemoteImageRequestState.Queued:
                    return "排队中";
                case RemoteImageRequestState.Downloading:
                    return "下载中";
                case RemoteImageRequestState.Retrying:
                    return "重试中";
                case RemoteImageRequestState.Succeeded:
                    return "成功";
                case RemoteImageRequestState.Failed:
                    return "失败";
                default:
                    return state.ToString();
            }
        }

        static string LocalizePriority(RemoteImagePriority priority)
        {
            return priority == RemoteImagePriority.High
                ? "高"
                : "低";
        }

        static string LocalizeSource(RemoteImageResultSource source)
        {
            switch (source)
            {
                case RemoteImageResultSource.MemoryCache:
                    return "内存缓存";
                case RemoteImageResultSource.DiskCache:
                    return "磁盘缓存";
                case RemoteImageResultSource.Network:
                    return "网络";
                default:
                    return "无";
            }
        }

        static string LocalizeWebRequestResult(
            UnityWebRequest.Result result)
        {
            switch (result)
            {
                case UnityWebRequest.Result.InProgress:
                    return "进行中";
                case UnityWebRequest.Result.Success:
                    return "成功";
                case UnityWebRequest.Result.ConnectionError:
                    return "连接错误";
                case UnityWebRequest.Result.ProtocolError:
                    return "协议错误";
                case UnityWebRequest.Result.DataProcessingError:
                    return "数据处理错误";
                default:
                    return result.ToString();
            }
        }

        void ConfirmAndClearCache(
            RemoteImageDeliveryClient client,
            CacheScan scan)
        {
            string root = client.GetSnapshot().CacheRoot;
            if (string.IsNullOrWhiteSpace(root))
            {
                EditorUtility.DisplayDialog(
                    "清空远程图片缓存",
                    "活动客户端没有提供缓存根目录。",
                    "确定");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "确定清空已提交的远程图片缓存？",
                "此操作会永久删除活动缓存中的所有已提交图片及其元数据。" +
                "为与原版一致，正在下载的 .part 文件不会被取消或删除。\n\n" +
                $"路径：\n{root}\n\n" +
                $"已提交图片：{scan.DataFiles} " +
                $"({FormatBytes(scan.DataBytes)})\n" +
                $"保留的未完成下载：{scan.PartialFiles} " +
                $"({FormatBytes(scan.PartialBytes)})\n\n" +
                "此操作无法撤销。",
                "删除缓存",
                "取消");
            if (!confirmed) return;

            int removed = client.ClearDiskCache();
            RefreshRuntimeSnapshot();
            ShowNotification(
                new GUIContent($"已删除 {removed} 张已提交缓存图片。"));
        }

        CacheScan ScanCache()
        {
            var result = new CacheScan();
            RemoteImageDeliveryClient active =
                RemoteImageDeliveryClient.Active;
            if (active != null)
            {
                try
                {
                    result.Root = active.GetSnapshot().CacheRoot;
                }
                catch (Exception ex)
                {
                    result.Error =
                        "无法读取活动缓存根目录：" + ex.Message;
                    return result;
                }
            }
            else
            {
                result.Root = ResolveConfiguredCacheRoot(out result.Error);
            }

            if (string.IsNullOrWhiteSpace(result.Root) ||
                !Directory.Exists(result.Root))
            {
                return result;
            }

            try
            {
                foreach (string path in Directory.GetFiles(
                             result.Root,
                             "*",
                             SearchOption.TopDirectoryOnly))
                {
                    long bytes = 0L;
                    try
                    {
                        bytes = new FileInfo(path).Length;
                    }
                    catch
                    {
                        // A runtime request may replace the file during scan.
                    }

                    if (path.EndsWith(
                            ".img",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        result.DataFiles++;
                        result.DataBytes += bytes;
                    }
                    else if (path.EndsWith(
                                 ".json",
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        result.MetadataFiles++;
                    }
                    else if (path.EndsWith(
                                 ".part",
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        result.PartialFiles++;
                        result.PartialBytes += bytes;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = "扫描缓存失败：" + ex.Message;
            }
            return result;
        }

        string ResolveConfiguredCacheRoot(out string error)
        {
            error = "";
            if (_config == null)
                return "";

            try
            {
                string persistentRoot =
                    Path.GetFullPath(Application.persistentDataPath)
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar);
                string child = (_config.cacheNamespace ?? "").Trim();
                if (child.Length == 0 ||
                    child == "." ||
                    child == ".." ||
                    child.IndexOfAny(
                        new[]
                        {
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar,
                            ':',
                        }) >= 0)
                {
                    error =
                        "配置的缓存命名空间不是安全的子目录名称。";
                    return "";
                }

                string path = Path.GetFullPath(
                    Path.Combine(persistentRoot, child));
                string expectedPrefix =
                    persistentRoot + Path.DirectorySeparatorChar;
                if (!path.StartsWith(
                        expectedPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    error =
                        "配置的缓存路径超出了 " +
                        "Application.persistentDataPath。";
                    return "";
                }
                return path;
            }
            catch (Exception ex)
            {
                error =
                    "无法解析配置的缓存路径：" + ex.Message;
                return "";
            }
        }

        static void OpenCacheFolder(string root)
        {
            try
            {
                Directory.CreateDirectory(root);
                EditorUtility.RevealInFinder(root);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "打开远程图片缓存",
                    "无法打开缓存目录：\n" + ex.Message,
                    "确定");
            }
        }

        void StartProbe(string url, RemoteImageAsset asset)
        {
            DisposeProbe();
            _probeSummary = "";
            _probeMessageType = MessageType.Info;
            try
            {
                _probeRequest = UnityWebRequest.Get(url);
                _probeRequest.timeout =
                    _config.cancelNetworkRequestOnTimeout
                        ? Mathf.Clamp(
                            _config.requestTimeoutSeconds,
                            3,
                            300)
                        : 0;
                if (!string.IsNullOrWhiteSpace(_config.acceptHeader))
                {
                    _probeRequest.SetRequestHeader(
                        "Accept",
                        _config.acceptHeader.Trim());
                }
                _probeWatch = Stopwatch.StartNew();
                _activeProbeAsset = asset.Clone();
                _probeOperation = _probeRequest.SendWebRequest();
            }
            catch (Exception ex)
            {
                DisposeProbe();
                _probeSummary = "无法开始下载：" + ex.Message;
                _probeMessageType = MessageType.Error;
            }
        }

        void CompleteProbe()
        {
            if (_probeRequest == null)
            {
                DisposeProbe();
                return;
            }

            _probeWatch?.Stop();
            long elapsed = _probeWatch?.ElapsedMilliseconds ?? 0L;
            try
            {
                byte[] bytes = _probeRequest.downloadHandler?.data ??
                               Array.Empty<byte>();
                RemoteImageAsset asset =
                    _activeProbeAsset ?? BuildProbeAsset();

                var lines = new List<string>
                {
                    $"HTTP {_probeRequest.responseCode}，耗时 {elapsed} 毫秒",
                    "结果：" + LocalizeWebRequestResult(_probeRequest.result),
                    "内容类型：" +
                    (_probeRequest.GetResponseHeader("Content-Type") ??
                     "（未提供）"),
                    "接收数据：" + FormatBytes(bytes.LongLength),
                };

                bool success =
                    _probeRequest.result ==
                    UnityWebRequest.Result.Success;
                if (!success)
                    lines.Add("错误：" + _probeRequest.error);

                if (success &&
                    asset.byteSize > 0 &&
                    bytes.LongLength != asset.byteSize)
                {
                    success = false;
                    lines.Add(
                        $"字节数不匹配：{bytes.LongLength}/" +
                        asset.byteSize);
                }

                if (success && !HasSupportedImageHeader(bytes))
                {
                    success = false;
                    lines.Add("图片文件头不受支持或文件已损坏。");
                }

                if (success &&
                    !string.IsNullOrWhiteSpace(asset.sha256))
                {
                    string actual = ComputeSha256(bytes);
                    string expected = NormalizeHash(asset.sha256);
                    bool hashMatches = string.Equals(
                        actual,
                        expected,
                        StringComparison.OrdinalIgnoreCase);
                    lines.Add(
                        "SHA-256: " +
                        (hashMatches ? "匹配" : "不匹配") +
                        $" ({actual})");
                    if (!hashMatches) success = false;
                }

                if (success)
                {
                    var texture = new Texture2D(
                        2,
                        2,
                        TextureFormat.RGBA32,
                        false);
                    try
                    {
                        if (!texture.LoadImage(bytes, true) ||
                            texture.width <= 4 ||
                            texture.height <= 4)
                        {
                            success = false;
                            lines.Add("Unity 无法解码此图片。");
                        }
                        else
                        {
                            lines.Add(
                                $"解码尺寸：{texture.width}x{texture.height}");
                        }
                    }
                    finally
                    {
                        DestroyImmediate(texture);
                    }
                }

                _probeSummary = string.Join("\n", lines);
                _probeMessageType = success
                    ? MessageType.Info
                    : MessageType.Error;
            }
            catch (Exception ex)
            {
                _probeSummary =
                    "诊断处理失败：" + ex.Message;
                _probeMessageType = MessageType.Error;
            }
            finally
            {
                DisposeProbe();
            }
        }

        void CancelProbe()
        {
            if (_probeRequest != null)
                _probeRequest.Abort();
            _probeSummary = "诊断下载已取消。";
            _probeMessageType = MessageType.Warning;
            DisposeProbe();
        }

        void DisposeProbe()
        {
            _probeOperation = null;
            _probeWatch?.Stop();
            _probeWatch = null;
            _probeRequest?.Dispose();
            _probeRequest = null;
            _activeProbeAsset = null;
        }

        RemoteImageAsset BuildProbeAsset()
        {
            return new RemoteImageAsset
            {
                key = _probeKey ?? "",
                relativePath = _probeRelativePath ?? "",
                absoluteUrl = _probeAbsoluteUrl ?? "",
                sha256 = _probeSha256 ?? "",
                byteSize = Math.Max(0L, _probeExpectedBytes),
                width = Math.Max(0, _probeWidth),
                height = Math.Max(0, _probeHeight),
                groupIndex = Math.Max(0, _probeGroup),
                itemIndex = Math.Max(0, _probeItem),
                variant = _probeVariant ?? "",
            };
        }

        void RefreshRuntimeSnapshot()
        {
            RemoteImageDeliveryClient client =
                RemoteImageDeliveryClient.Active;
            if (client == null)
            {
                _snapshot = null;
                _snapshotError = "";
                return;
            }

            try
            {
                _snapshot = client.GetSnapshot();
                _snapshotError = "";
            }
            catch (Exception ex)
            {
                _snapshot = null;
                _snapshotError =
                    "无法读取运行时快照：" + ex.Message;
            }
        }

        void SetConfiguration(RemoteImageDeliveryConfig config)
        {
            _config = config;
            _serializedConfig =
                config != null ? new SerializedObject(config) : null;
            RememberConfiguration(config);
            RefreshRuntimeSnapshot();
            Repaint();
        }

        void EnsureSerializedConfiguration()
        {
            if (_config != null &&
                (_serializedConfig == null ||
                 _serializedConfig.targetObject != _config))
            {
                _serializedConfig = new SerializedObject(_config);
            }
        }

        void LoadRememberedConfiguration()
        {
            string guid = EditorPrefs.GetString(
                ConfigGuidPreference,
                "");
            if (string.IsNullOrEmpty(guid)) return;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;
            _config =
                AssetDatabase.LoadAssetAtPath<RemoteImageDeliveryConfig>(
                    path);
            if (_config != null)
                _serializedConfig = new SerializedObject(_config);
        }

        static void RememberConfiguration(
            RemoteImageDeliveryConfig config)
        {
            if (config == null)
            {
                EditorPrefs.DeleteKey(ConfigGuidPreference);
                return;
            }
            string path = AssetDatabase.GetAssetPath(config);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
                EditorPrefs.SetString(ConfigGuidPreference, guid);
        }

        static bool TryGetHttpUri(string value, out Uri uri)
        {
            uri = null;
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri parsed))
                return false;
            if (!string.Equals(
                    parsed.Scheme,
                    Uri.UriSchemeHttp,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    parsed.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            uri = parsed;
            return true;
        }

        static bool HasSupportedImageHeader(byte[] bytes)
        {
            if (bytes == null) return false;
            bool jpeg = bytes.Length >= 3 &&
                        bytes[0] == 0xFF &&
                        bytes[1] == 0xD8 &&
                        bytes[2] == 0xFF;
            bool png = bytes.Length >= 8 &&
                       bytes[0] == 0x89 &&
                       bytes[1] == 0x50 &&
                       bytes[2] == 0x4E &&
                       bytes[3] == 0x47 &&
                       bytes[4] == 0x0D &&
                       bytes[5] == 0x0A &&
                       bytes[6] == 0x1A &&
                       bytes[7] == 0x0A;
            bool webp = bytes.Length >= 12 &&
                        bytes[0] == (byte)'R' &&
                        bytes[1] == (byte)'I' &&
                        bytes[2] == (byte)'F' &&
                        bytes[3] == (byte)'F' &&
                        bytes[8] == (byte)'W' &&
                        bytes[9] == (byte)'E' &&
                        bytes[10] == (byte)'B' &&
                        bytes[11] == (byte)'P';
            return jpeg || png || webp;
        }

        static string ComputeSha256(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes ?? Array.Empty<byte>());
            return BitConverter.ToString(hash)
                .Replace("-", "")
                .ToLowerInvariant();
        }

        static string NormalizeHash(string value)
        {
            return (value ?? "")
                .Trim()
                .Replace("-", "")
                .ToLower(CultureInfo.InvariantCulture);
        }

        static string FormatBytes(long value)
        {
            double bytes = Math.Max(0L, value);
            string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
            int unit = 0;
            while (bytes >= 1024d && unit < units.Length - 1)
            {
                bytes /= 1024d;
                unit++;
            }
            return unit == 0
                ? $"{bytes:0} {units[unit]}"
                : $"{bytes:0.0} {units[unit]}";
        }

        static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return "";
        }
    }
}
