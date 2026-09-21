using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RemoteImageDelivery.Editor
{
    internal static class RemoteImageDeliveryEditorLabels
    {
        static readonly Dictionary<string, GUIContent> Labels =
            new Dictionary<string, GUIContent>
            {
                {
                    "remoteEnabled",
                    new GUIContent(
                        "启用远程图片",
                        "远程下载总开关。关闭后仍可使用内置或本地回退资源。")
                },
                {
                    "baseUrl",
                    new GUIContent(
                        "CDN 基础地址",
                        "例如：https://cdn.example.com/game/prod/v1")
                },
                {
                    "contentRoot",
                    new GUIContent(
                        "内容根路径",
                        "可选。插入在 CDN 基础地址与图片相对路径之间。")
                },
                {
                    "urlMode",
                    new GUIContent(
                        "URL 解析模式",
                        "决定优先使用 CDN 相对路径、原始绝对地址还是自定义模板。")
                },
                {
                    "urlTemplate",
                    new GUIContent(
                        "URL 模板",
                        "支持占位符：{base}、{root}、{path}、{url}、{width}、{height}。")
                },
                {
                    "allowAbsoluteFallbackUrl",
                    new GUIContent(
                        "允许绝对地址回退",
                        "只有图片没有可用 CDN 相对路径时，才使用原始绝对地址。")
                },
                {
                    "resizeAbsoluteFallbackWithImageView",
                    new GUIContent(
                        "旧源站 ImageView 缩放",
                        "用于兼容还原出的 BubblePics 旧源站；Cloudflare 静态文件通常不需要。")
                },
                {
                    "acceptHeader",
                    new GUIContent(
                        "Accept 请求头",
                        "下载图片时发送的 HTTP Accept 请求头。")
                },
                {
                    "itemsPerGroup",
                    new GUIContent(
                        "每组关卡数",
                        "BubblePics 原版按 25 关划分一个预取组。")
                },
                {
                    "highPriorityConcurrency",
                    new GUIContent(
                        "高优先级并发数",
                        "当前关卡和紧邻下一关的最大并发下载数。")
                },
                {
                    "lowPriorityConcurrency",
                    new GUIContent(
                        "低优先级并发数",
                        "后台预取后续关卡图片的最大并发下载数。")
                },
                {
                    "cacheDecodesPerFrame",
                    new GUIContent(
                        "每帧缓存解码数",
                        "每个渲染帧最多从磁盘读取并完整解码多少张缓存图片。建议保持 1，避免首次进入关卡时批量解码卡住主线程。")
                },
                {
                    "foregroundMaxAttempts",
                    new GUIContent(
                        "前台最大尝试次数",
                        "进入关卡所需图片下载失败后的最大尝试次数。")
                },
                {
                    "backgroundMaxAttempts",
                    new GUIContent(
                        "后台最大尝试次数",
                        "后台预取图片下载失败后的最大尝试次数。")
                },
                {
                    "cancelNetworkRequestOnTimeout",
                    new GUIContent(
                        "中止超时网络请求",
                        "关闭时与原版一致：前台等待到期后继续游戏回退流程，但底层请求仍可在后台完成并落入缓存。")
                },
                {
                    "requestTimeoutSeconds",
                    new GUIContent(
                        "单次请求超时（秒）",
                        "仅在“中止超时网络请求”开启时生效。")
                },
                {
                    "foregroundBatchTimeoutSeconds",
                    new GUIContent(
                        "前台批次总超时（秒）",
                        "进入关卡时整批图片允许阻塞加载界面的最长时间。")
                },
                {
                    "retryBaseDelaySeconds",
                    new GUIContent(
                        "重试基础延迟（秒）",
                        "下载失败后再次尝试前的基础等待时间。")
                },
                {
                    "retryJitter",
                    new GUIContent(
                        "重试随机抖动",
                        "在重试延迟中加入随机量，避免大量请求同时重试。")
                },
                {
                    "prefetchNextItemHigh",
                    new GUIContent(
                        "高优先级预取下一关",
                        "当前关卡加载后，将下一关图片加入高优先级队列。")
                },
                {
                    "prefetchCurrentAndNextGroup",
                    new GUIContent(
                        "预取当前组和下一组",
                        "按照关卡组提前缓存当前组以及下一组图片。")
                },
                {
                    "advancePrefetchWindow",
                    new GUIContent(
                        "向前预取窗口",
                        "从当前关开始，额外向后预取多少关。")
                },
                {
                    "nextGroupThreshold",
                    new GUIContent(
                        "下一组触发阈值",
                        "距离本组结束小于该值时，将下一组元数据纳入已知保留集合；图片仍只按前向窗口下载。")
                },
                {
                    "pruneItemsBeforeCurrent",
                    new GUIContent(
                        "过关后删除过去图片",
                        "仅在点击“下一关”且新关卡成功打开、前向预取窗口完成后，删除已知保留集合之外的已提交图片。")
                },
                {
                    "bundledSeedItemCount",
                    new GUIContent(
                        "内置种子关卡数",
                        "随安装包提供图片的前置关卡数量；还原出的原版为前 10 关。")
                },
                {
                    "preferRemoteAfterSeedItems",
                    new GUIContent(
                        "种子关卡后优先远程",
                        "开启后，种子关卡之后优先测试 CDN，即使开发目录仍存在本地图片。")
                },
                {
                    "cacheNamespace",
                    new GUIContent(
                        "缓存目录名称",
                        "Application.persistentDataPath 下安全的子目录名称。")
                },
                {
                    "memoryTextureLimit",
                    new GUIContent(
                        "内存纹理上限",
                        "内存中最多保留的已解码纹理数量。")
                },
                {
                    "enforceDiskCacheBudget",
                    new GUIContent(
                        "自动执行容量淘汰",
                        "原版默认关闭。开启后每次成功下载都会按磁盘上限执行 LRU 清理；关闭后仍可手动整理。")
                },
                {
                    "maxDiskCacheMiB",
                    new GUIContent(
                        "磁盘缓存上限（MiB）",
                        "仅供自动容量淘汰或监控面板的手动容量整理使用。原版兼容模式不会自动套用此上限。")
                },
                {
                    "resumePartialDownloads",
                    new GUIContent(
                        "启用断点续传",
                        "原版默认关闭。仅在 CDN 稳定支持 Range 请求时建议开启。")
                },
                {
                    "resumeMinimumBytes",
                    new GUIContent(
                        "断点续传最小字节数",
                        "文件达到此大小后才尝试保留并续传未完成内容。")
                },
                {
                    "validateImageHeader",
                    new GUIContent(
                        "验证图片文件头",
                        "检查下载内容是否具有合法的 JPEG、PNG 或 WebP 文件头。")
                },
                {
                    "validateDecodedTexture",
                    new GUIContent(
                        "验证纹理解码",
                        "确认 Unity 能将下载字节成功解码为有效纹理。")
                },
                {
                    "verifySha256WhenProvided",
                    new GUIContent(
                        "校验 SHA-256",
                        "资源清单提供 SHA-256 时，验证下载内容是否完全一致。")
                },
                {
                    "verifyByteCountWhenProvided",
                    new GUIContent(
                        "校验文件大小",
                        "资源清单提供字节数时，验证下载文件大小是否一致。")
                },
                {
                    "verboseLogging",
                    new GUIContent(
                        "详细日志",
                        "在 Console 输出更详细的远程图片请求过程。")
                },
                {
                    "keepRequestHistory",
                    new GUIContent(
                        "保留请求历史",
                        "记录成功、缓存命中和失败的请求，供监控面板查看。")
                },
                {
                    "requestHistoryLimit",
                    new GUIContent(
                        "请求历史上限",
                        "内存中最多保留多少条已完成请求记录。")
                },
            };

        static readonly string[] UrlModeLabels =
        {
            "相对路径优先，失败时用绝对地址",
            "仅使用绝对地址",
            "仅使用相对路径",
            "使用 URL 模板",
        };

        public static GUIContent Get(string propertyName)
        {
            return Labels.TryGetValue(propertyName, out GUIContent content)
                ? content
                : new GUIContent(ObjectNames.NicifyVariableName(propertyName));
        }

        public static void DrawProperty(SerializedProperty property)
        {
            if (property == null) return;
            GUIContent content = Get(property.name);
            if (property.name == "urlMode")
            {
                property.enumValueIndex = EditorGUILayout.Popup(
                    content,
                    property.enumValueIndex,
                    UrlModeLabels);
                return;
            }
            EditorGUILayout.PropertyField(property, content, true);
        }
    }

    [CustomEditor(typeof(RemoteImageDeliveryConfig))]
    public sealed class RemoteImageDeliveryConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(
                            iterator,
                            new GUIContent("脚本"),
                            true);
                    }
                    continue;
                }

                if (iterator.name == "remoteEnabled")
                    DrawHeader("CDN 端点");
                else if (iterator.name == "itemsPerGroup")
                    DrawHeader("原版兼容调度");
                else if (iterator.name == "cacheNamespace")
                    DrawHeader("缓存");
                else if (iterator.name == "verboseLogging")
                    DrawHeader("诊断");

                RemoteImageDeliveryEditorLabels.DrawProperty(iterator);
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开远程图片监控面板"))
                    RemoteImageDeliveryWindow.Open();

                if (GUILayout.Button("规范化并保存"))
                {
                    var config = (RemoteImageDeliveryConfig)target;
                    Undo.RecordObject(config, "规范化远程图片配置");
                    config.Sanitize();
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        static void DrawHeader(string title)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }

    public static class RemoteImageDeliveryConfigAssetMenu
    {
        const string DefaultDirectory = "Assets/Resources";
        const string DefaultName = "RemoteImageDeliveryConfig.asset";

        [MenuItem(
            "Tools/远程图片分发/创建默认配置",
            priority = 100)]
        public static void CreateDefaultConfigurationMenu()
        {
            CreateDefaultConfiguration();
        }

        public static RemoteImageDeliveryConfig CreateDefaultConfiguration()
        {
            const string path =
                DefaultDirectory + "/" + DefaultName;
            RemoteImageDeliveryConfig existing =
                AssetDatabase.LoadAssetAtPath<RemoteImageDeliveryConfig>(
                    path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return existing;
            }
            Object occupied = AssetDatabase.LoadMainAssetAtPath(path);
            if (occupied != null)
            {
                EditorUtility.DisplayDialog(
                    "创建远程图片分发配置",
                    "默认路径已被其他资源占用：\n\n" +
                    path +
                    "\n\n请先移动或重命名该资源，然后重新执行此命令。",
                    "确定");
                Selection.activeObject = occupied;
                EditorGUIUtility.PingObject(occupied);
                return null;
            }

            EnsureAssetDirectory(DefaultDirectory);

            var config =
                ScriptableObject.CreateInstance<RemoteImageDeliveryConfig>();
            config.Sanitize();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            return config;
        }

        static void EnsureAssetDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) ||
                AssetDatabase.IsValidFolder(directory))
            {
                return;
            }

            string[] segments = directory.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
