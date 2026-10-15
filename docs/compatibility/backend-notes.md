# MediaDevices 2.0.0 backend inspection

Inspected the restored NuGet package's XML and public assembly surface on 2026-10-15, with upstream source as supporting evidence. The package is the dependency; no library source is copied into the application.

- Discovery: `MediaDeviceManager.Instance.GetDevices()`; the manager owns the worker thread and is disposed once at process shutdown.
- Connection: `MediaDevice.Connect(MediaDeviceAccess.GenericRead, ...)`; no default read/write access.
- Traversal: `GetRootDirectory()` and `MediaDirectoryInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly)`. The explicit search overload avoids the upstream no-argument child iterator's broad exception suppression. Functional storage objects require traversal as containers, not images.
- Identity: `DeviceId`, `PnPDeviceID`, `SerialNumber`, plus public friendly-name/manufacturer/model/protocol properties. Serial and PnP identifiers are candidates requiring reconnect validation.
- Photo properties: `Id`, `PersistentUniqueId`, `FullName`, `Name`, `Length`, `DateAuthored`, `CreationTime`, and `LastWriteTime`.
- The public photo surface does not expose the underlying PROPVARIANT, reported content type/format, or a separate PTP capture date. Nullable DateTime properties are projections; they cannot prove the source timezone or raw representation. Null is reported as unavailable, not evidence that the phone never supplies that property through WPD.
- The size surface is a nonnullable ulong and cannot distinguish an absent size property from zero. The adapter treats zero as unknown rather than certifying an empty image.

The initial adapter reports these evidence limitations explicitly, with null raw values, unknown timezone evidence, missing PTP date, and null format/content type. It does not infer those fields from the extension or mark projected DateTime.Kind as original timezone evidence. No raw-property COM reader is added in this implementation. If physical validation needs those unavailable properties, that phone remains unaccepted pending a narrow WPD extension or another backend.

The upstream library may skip objects whose property construction raises FileNotFoundException; it also discards the initial EnumObjects HRESULT and later Next HRESULTs in the search-pattern child iterator. A native access/disconnect error can therefore appear as a clean empty branch without reaching this adapter. The application cannot recover those hidden errors through the public API. A clean application report therefore means enumeration completed through this API, not independently verified inventory completeness. Reports visibly warn about this limitation. Expected folder/photo counts must be compared on actual hardware; the backend is not accepted for synchronization on the strength of a clean report alone.

On the first actual Samsung S21 probe, the package surfaced `0x800701b1` (device does not exist) wrapped in AggregateException by ThreadSafeWorker. The boundary now unwraps those worker exceptions, treats known device-removed codes as terminal, retains the native diagnostic in the report, and normalizes other package errors to IO failures. Children are consumed as a stream so observations already returned by the library survive subsequent folder errors or cancellation. Disconnect cleanup reports a warning without replacing the main probe result.

Synchronous package calls cannot be interrupted mid-COM-call by this adapter; Ctrl+C takes effect between calls. No content stream, icon, thumbnail, download, setter, or mutation API is called.

Sources: [NuGet package](https://www.nuget.org/packages/MediaDevices/2.0.0), [public file metadata implementation](https://github.com/Bassman2/MediaDevices/blob/main/Src/MediaDevicesShare/MediaFileSystemInfo.cs), [internal property/enumeration implementation](https://github.com/Bassman2/MediaDevices/blob/main/Src/MediaDevicesShare/Internal/Item.cs), [device connection](https://github.com/Bassman2/MediaDevices/blob/main/Src/MediaDevicesShare/MediaDevice.cs).
