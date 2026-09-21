using System;
using UnityEngine;

namespace RemoteImageDelivery
{
    public enum RemoteUrlMode
    {
        RelativePathThenAbsoluteFallback = 0,
        AbsoluteUrlOnly = 1,
        RelativePathOnly = 2,
        Template = 3,
    }

    /// <summary>
    /// Project-independent runtime policy. Keep credentials out of this asset:
    /// only public delivery URLs and client-safe headers belong here.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RemoteImageDeliveryConfig",
        menuName = "远程图片分发/配置")]
    public sealed class RemoteImageDeliveryConfig : ScriptableObject
    {
        [Header("CDN 端点")]
        [Tooltip("远程下载总开关。关闭后仍可使用内置或本地回退资源。")]
        public bool remoteEnabled = true;
        [Tooltip("例如：https://cdn.example.com/game/prod/v1")]
        public string baseUrl = "";
        [Tooltip("可选。插入在 CDN 基础地址与每个资源相对路径之间。")]
        public string contentRoot = "";
        public RemoteUrlMode urlMode =
            RemoteUrlMode.RelativePathThenAbsoluteFallback;
        [Tooltip(
            "模板占位符：{base}、{root}、{path}、{url}、{width}、{height}。")]
        public string urlTemplate = "{base}/{root}/{path}";
        [Tooltip("只有资源没有 CDN 相对路径时，才使用原始绝对地址。")]
        public bool allowAbsoluteFallbackUrl = true;
        [Tooltip(
            "用于兼容还原出的 BubblePics 旧源站；Cloudflare 静态文件通常应关闭。")]
        public bool resizeAbsoluteFallbackWithImageView;
        [Tooltip("下载图片时发送的 HTTP Accept 请求头。")]
        public string acceptHeader = "image/webp";

        [Header("原版兼容调度")]
        [Min(1)] public int itemsPerGroup = 25;
        [Min(1)] public int highPriorityConcurrency = 10;
        [Min(1)] public int lowPriorityConcurrency = 5;
        [Tooltip(
            "每个渲染帧最多从磁盘读取并完整解码多少张缓存图片。建议保持 1，避免首次进入关卡时批量解码阻塞主线程。")]
        [Min(1)] public int cacheDecodesPerFrame = 1;
        [Min(1)] public int foregroundMaxAttempts = 1;
        [Min(1)] public int backgroundMaxAttempts = 1;
        [Tooltip(
            "开启后由 UnityWebRequest 在单次请求超时时主动中止下载。还原出的原版不会中止底层请求，而只让前台加载等待 20 秒。")]
        public bool cancelNetworkRequestOnTimeout;
        [Min(3)] public int requestTimeoutSeconds = 20;
        [Min(10)] public int foregroundBatchTimeoutSeconds = 20;
        [Min(0f)] public float retryBaseDelaySeconds = 0.75f;
        [Range(0f, 0.5f)] public float retryJitter = 0.15f;
        public bool prefetchNextItemHigh = true;
        public bool prefetchCurrentAndNextGroup = true;
        [Min(1)] public int advancePrefetchWindow = 11;
        [Min(1)] public int nextGroupThreshold = 5;
        public bool pruneItemsBeforeCurrent = true;
        [Tooltip(
            "还原出的原版会在安装包中提供前 10 关图片。")]
        [Min(0)] public int bundledSeedItemCount = 10;
        [Tooltip(
            "开启后，项目适配器可以在种子范围之后忽略开发目录中的本地副本，以便在不删除源文件的情况下测试 CDN。")]
        public bool preferRemoteAfterSeedItems;

        [Header("缓存")]
        [Tooltip("Application.persistentDataPath 下安全的子目录名称。")]
        public string cacheNamespace = "remote_images_v1";
        [Min(8)] public int memoryTextureLimit = 48;
        [Tooltip(
            "开启后，每次下载成功都会按下面的容量上限自动执行 LRU 整理。还原出的原版没有容量淘汰，默认关闭；仍可在监控面板手动整理。")]
        public bool enforceDiskCacheBudget;
        [Min(16)] public int maxDiskCacheMiB = 1024;
        [Tooltip(
            "关闭时与还原出的原版一致。仅对可靠支持 Range 请求的大文件 CDN 建议开启。")]
        public bool resumePartialDownloads;
        [Min(0)] public int resumeMinimumBytes = 512 * 1024;
        public bool validateImageHeader = true;
        public bool validateDecodedTexture = true;
        public bool verifySha256WhenProvided = true;
        public bool verifyByteCountWhenProvided = true;

        [Header("诊断")]
        public bool verboseLogging;
        public bool keepRequestHistory = true;
        [Range(20, 1000)] public int requestHistoryLimit = 200;

        public void Sanitize()
        {
            baseUrl = NormalizeUrlPart(baseUrl);
            contentRoot = NormalizeRelativePart(contentRoot);
            cacheNamespace = SanitizeDirectory(cacheNamespace);
            highPriorityConcurrency = Mathf.Clamp(
                highPriorityConcurrency, 1, 32);
            lowPriorityConcurrency = Mathf.Clamp(
                lowPriorityConcurrency, 1, 32);
            cacheDecodesPerFrame = Mathf.Clamp(
                cacheDecodesPerFrame, 1, 32);
            foregroundMaxAttempts = Mathf.Clamp(
                foregroundMaxAttempts, 1, 8);
            backgroundMaxAttempts = Mathf.Clamp(
                backgroundMaxAttempts, 1, 8);
            requestTimeoutSeconds = Mathf.Clamp(
                requestTimeoutSeconds, 3, 300);
            foregroundBatchTimeoutSeconds = Mathf.Clamp(
                foregroundBatchTimeoutSeconds, 10, 600);
            retryBaseDelaySeconds = Mathf.Clamp(
                retryBaseDelaySeconds, 0f, 30f);
            retryJitter = Mathf.Clamp(retryJitter, 0f, 0.5f);
            itemsPerGroup = Mathf.Clamp(itemsPerGroup, 1, 1000);
            advancePrefetchWindow = Mathf.Clamp(
                advancePrefetchWindow, 1, 1000);
            nextGroupThreshold = Mathf.Clamp(
                nextGroupThreshold, 1, itemsPerGroup);
            bundledSeedItemCount = Mathf.Max(0, bundledSeedItemCount);
            memoryTextureLimit = Mathf.Clamp(memoryTextureLimit, 8, 512);
            maxDiskCacheMiB = Mathf.Clamp(maxDiskCacheMiB, 16, 32768);
            resumeMinimumBytes = Mathf.Max(0, resumeMinimumBytes);
            requestHistoryLimit = Mathf.Clamp(requestHistoryLimit, 20, 1000);
            acceptHeader = string.IsNullOrWhiteSpace(acceptHeader)
                ? "image/webp"
                : acceptHeader.Trim();
        }

        public string ResolveUrl(RemoteImageAsset asset)
        {
            if (asset == null) return "";
            string relative = NormalizeRelativePart(asset.relativePath);
            string absolute = NormalizeUrlPart(asset.absoluteUrl);
            string basePart = NormalizeUrlPart(baseUrl);
            string root = NormalizeRelativePart(contentRoot);

            string resolved;
            switch (urlMode)
            {
                case RemoteUrlMode.AbsoluteUrlOnly:
                    resolved = absolute;
                    break;
                case RemoteUrlMode.RelativePathOnly:
                    resolved = JoinUrl(basePart, root, relative);
                    break;
                case RemoteUrlMode.Template:
                    resolved = (urlTemplate ?? "")
                        .Replace("{base}", basePart)
                        .Replace("{root}", root)
                        .Replace("{path}", relative)
                        .Replace("{url}", absolute)
                        .Replace("{width}", Mathf.Max(0, asset.width).ToString())
                        .Replace("{height}", Mathf.Max(0, asset.height).ToString());
                    resolved = CollapseUrlSlashes(resolved);
                    break;
                default:
                    resolved = !string.IsNullOrEmpty(basePart) &&
                               !string.IsNullOrEmpty(relative)
                        ? JoinUrl(basePart, root, relative)
                        : allowAbsoluteFallbackUrl
                            ? absolute
                            : "";
                    break;
            }

            if (resizeAbsoluteFallbackWithImageView &&
                string.Equals(resolved, absolute, StringComparison.Ordinal) &&
                asset.width > 0 &&
                resolved.IndexOf(
                    "/imageView/",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                resolved = AddImageViewSize(
                    resolved,
                    asset.width,
                    asset.height > 0 ? asset.height : asset.width);
            }
            return resolved;
        }

        public bool IsConfiguredForRemote(out string reason)
        {
            Sanitize();
            if (!remoteEnabled)
            {
                reason = "远程图片分发已关闭。";
                return false;
            }
            if (urlMode != RemoteUrlMode.AbsoluteUrlOnly &&
                urlMode != RemoteUrlMode.Template &&
                string.IsNullOrEmpty(baseUrl) &&
                !allowAbsoluteFallbackUrl)
            {
                reason = "未配置 CDN 基础地址或绝对地址回退。";
                return false;
            }
            reason = "";
            return true;
        }

        static string AddImageViewSize(string rawUrl, int width, int height)
        {
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri uri))
                return rawUrl;
            string path = uri.AbsolutePath.TrimStart('/');
            return uri.GetLeftPart(UriPartial.Authority) +
                   $"/imageView/{width}/{height}/{path}{uri.Query}";
        }

        static string JoinUrl(params string[] values)
        {
            string output = "";
            foreach (string raw in values)
            {
                string value = raw ?? "";
                if (value.Length == 0) continue;
                output = output.Length == 0
                    ? value.TrimEnd('/')
                    : output.TrimEnd('/') + "/" + value.Trim('/');
            }
            return output;
        }

        static string CollapseUrlSlashes(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string trimmed = value.Trim();
            int scheme = trimmed.IndexOf("://", StringComparison.Ordinal);
            if (scheme < 0)
            {
                while (trimmed.Contains("//"))
                    trimmed = trimmed.Replace("//", "/");
                return trimmed;
            }
            string prefix = trimmed.Substring(0, scheme + 3);
            string rest = trimmed.Substring(scheme + 3);
            while (rest.Contains("//"))
                rest = rest.Replace("//", "/");
            return prefix + rest;
        }

        static string NormalizeUrlPart(string value)
        {
            return (value ?? "").Trim().TrimEnd('/');
        }

        public static string NormalizeRelativePart(string value)
        {
            string clean = (value ?? "").Trim().Replace('\\', '/');
            while (clean.StartsWith("/", StringComparison.Ordinal))
                clean = clean.Substring(1);
            while (clean.Contains("//"))
                clean = clean.Replace("//", "/");
            if (clean == "." || clean == ".." ||
                clean.StartsWith("../", StringComparison.Ordinal) ||
                clean.Contains("/../"))
            {
                return "";
            }
            return clean.TrimEnd('/');
        }

        static string SanitizeDirectory(string value)
        {
            string clean = (value ?? "").Trim()
                .Replace('\\', '_')
                .Replace('/', '_')
                .Replace(':', '_');
            if (clean.Length == 0 || clean == "." || clean == "..")
                return "remote_images_v1";
            return clean;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            Sanitize();
        }
#endif
    }
}
