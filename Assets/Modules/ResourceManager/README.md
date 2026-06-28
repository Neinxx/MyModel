# ResourceManager

ResourceManager is a small resource facade for Unity modules. It keeps the core cache and handle lifecycle independent from Addressables, while optional providers can be layered in by assembly.

## Runtime Core

- Assembly: `ResourceManager.Runtime`
- Root namespace: `ResourceManagerModule.Runtime`
- Core dependencies: UnityEngine only.
- Default providers:
  - `DirectRefProvider` for tests, demos, and manually registered assets.
  - `ResourcesProvider` as an optional fallback controlled by `ResourceManager.EnableResourcesFallback`.
- Main API:

```csharp
using ResourceManagerModule.Runtime;
using UnityEngine;

ResourceHandle<GameObject> handle = await ResourceManager.LoadAsync<GameObject>("PlayerPrefab");
GameObject prefab = handle.Asset;
handle.Dispose();
```

Prefer disposing handles with `using` or explicit `Dispose()`. The callback overload exists only for compatibility and should not be used for new code.

## Addressables

- Assembly: `ResourceManager.Addressables`
- Optional dependency: Addressables package.
- `AddressablesProvider` registers before `ResourcesProvider` and initializes Addressables asynchronously.
- `CanLoad` never blocks on Addressables. It uses `AddressablesResourceCatalog` first, then falls back to already available locators.

Generate the catalog from the Unity menu:

```text
Tools/Resource Manager/Rebuild Addressables Catalog
```

The generated asset path is:

```text
Assets/Resources/AddressablesResourceCatalog.asset
```

## Editor

- Assembly: `ResourceManager.Addressables.Editor`
- Editor-only responsibilities are limited to rebuilding the Addressables catalog asset.
- Runtime assemblies must not reference `UnityEditor`, `AssetDatabase`, `MenuItem`, or Addressables editor APIs.

## Tests

- Runtime tests: `ResourceManager.Tests`
- Editor tests: `ResourceManager.Editor.Tests`
- Coverage focuses on reference counting, provider binding, strict fallback behavior, catalog lookup, and catalog key normalization.
- Command line:

```bash
Tools/CI/run-resource-manager-editmode-tests.sh
```

The command writes:

- `Logs/TestResults/resource-manager-editmode-results.xml`
- `Logs/TestResults/resource-manager-editmode.log`

## Rules

- Keep the core assembly portable.
- Keep Addressables optional.
- Keep runtime logs quiet by default; use `ResourceManager.VerboseLogging` only for diagnostics.
- Treat `AddressablesResourceCatalog` as generated data, not gameplay configuration.
