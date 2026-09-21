# Remote Image Delivery

Reusable remote-image delivery for Unity 2022.3. The package provides:

- separate HIGH and LOW priority queues;
- request coalescing by stable asset key;
- persistent and in-memory caches;
- resumable `.part` downloads and atomic cache commits;
- image-header, decoded-texture, byte-count, and SHA-256 validation;
- original-compatible grouped prefetch behavior;
- a live Editor dashboard for configuration, queue/history inspection, cache
  management, and URL diagnostics.

The runtime never creates a hidden scene object. A project supplies an authored
`MonoBehaviour` host when constructing `RemoteImageDeliveryClient`.

## Quick setup

1. Open **Tools > 远程图片分发 > 监控面板**.
2. Click **创建默认配置**. The package creates or selects
   `Assets/Resources/RemoteImageDeliveryConfig.asset`, which can be loaded as
   `Resources.Load<RemoteImageDeliveryConfig>("RemoteImageDeliveryConfig")`.
3. Configure the public CDN base URL and content root. Never put Cloudflare API
   tokens, signing secrets, or other credentials in this asset.
4. Add an authored host component or prefab to the project's bootstrap scene.
5. Construct the client from that host:

```csharp
using RemoteImageDelivery;
using UnityEngine;

public sealed class RemoteImagesHost : MonoBehaviour
{
    [SerializeField] RemoteImageDeliveryConfig config;
    public RemoteImageDeliveryClient Client { get; private set; }

    void Awake()
    {
        Client = new RemoteImageDeliveryClient(this, config);
    }

    void OnDestroy()
    {
        Client?.Dispose();
    }
}
```

Project adapters can wrap their level, card, episode, or chapter data as
`RemoteImageAsset`. Use `RemoteImageBatchPrefetcher` when the content is ordered
into fixed-size groups.

## Dashboard

Open **Tools > 远程图片分发 > 监控面板**.

### Configuration

Edits every field of `RemoteImageDeliveryConfig` in clear Endpoint, Key Player
Delivery Gates, Scheduling, Cache, and Diagnostics sections. The three primary
delivery gates are intentionally prominent:

- `bundledSeedItemCount`: how many first items belong to the built-in seed;
- `preferRemoteAfterSeedItems`: lets a project adapter bypass development
  copies after the seed so CDN behavior is testable;
- `foregroundBatchTimeoutSeconds`: maximum wait for a foreground image batch.

Original-compatible projects should leave
`cancelNetworkRequestOnTimeout`, `enforceDiskCacheBudget`, and
`resumePartialDownloads` disabled. In that mode the foreground may stop waiting
after 20 seconds without aborting the underlying download, failed partial files
are removed immediately, and successful downloads are not subject to an
automatic size-based LRU.

The selected asset is remembered per editor user. **Use Runtime Config**
switches the dashboard to the configuration used by the currently active
client.

### Runtime Queue & History

While Play Mode has an active client, this tab shows:

- active and queued HIGH/LOW request counts;
- unique in-flight requests;
- session network successes, failures, bytes, and cache hits;
- searchable completed-request history, including URL, source, attempts,
  duration, cache path, and error.

The dashboard only observes `RemoteImageDeliveryClient.Active`; it does not
create a runtime client.

### Cache

Shows the exact persistent cache root, committed image count and size, metadata,
partial downloads, and configured budget. It can open the cache directory,
prune through `PruneToBudget`, and clear the client's memory lookup.

**Clear Committed Disk Cache** is enabled only when an active client provides
the authoritative cache root. It displays the exact target and requires
explicit confirmation before calling `ClearDiskCache`. In original-compatible
mode this removes committed `.img` files and their sidecars but deliberately
leaves in-flight `.img.part` files alone.

### URL Diagnostics

Builds a sample `RemoteImageAsset`, previews the URL returned by
`RemoteImageDeliveryConfig.ResolveUrl`, and can copy, open, or test it.

The diagnostic test performs a GET with the configured Accept header and
timeout, then checks:

- HTTP result and status code;
- expected byte count when provided;
- SHA-256 when provided;
- JPEG, PNG, or WebP file signature;
- Unity `Texture2D` decoding and decoded dimensions.

Probe bytes remain in memory and never enter the runtime cache.

## Configuration creation

A default configuration is created at
`Assets/Resources/RemoteImageDeliveryConfig.asset`. If it already exists, the
creation command selects it instead of making a duplicate. Use either:

- **Tools > 远程图片分发 > 创建默认配置**
- the dashboard's **创建默认配置** button;

Use **Assets > Create > Remote Image Delivery > Configuration** only when a
project intentionally needs an additional configuration at another path.

## CDN guidance

Prefer versioned, immutable relative paths in manifests, for example:

```text
https://cdn.example.com/game/prod/2026.07.29/images/512/<hash>.jpg
```

Set `baseUrl` to the public CDN origin and `contentRoot` to the project/channel
version. Keep provider credentials in CI or publishing tools, never in a runtime
configuration asset.

For a static Cloudflare or R2 custom domain, use
`RelativePathThenAbsoluteFallback` or `RelativePathOnly`. Use `Template` only
when a Worker or image-resizing endpoint requires a custom URL shape.

## Cache safety

`cacheNamespace` is sanitized into one child directory below
`Application.persistentDataPath`. The dashboard refuses to guess a destructive
target when no active client exists. Cache deletion cannot be undone.

Progression pruning enumerates committed `.img` files directly, so images
without a JSON sidecar are not missed. It retains only keys belonging to known
current/future items (including alternate project-declared variants), never
deletes `.part`, and runs only after the next item opens successfully and its
advance-prefetch window completes. Stale `.part` files are purged on the next
startup when resume support is disabled.

Run **Tools > 远程图片分发 > 验证原版缓存生命周期** to exercise corrupt-file
deletion, metadata-less image pruning, manual-clear partial preservation, and
startup partial cleanup without adding the Unity Test Framework.
