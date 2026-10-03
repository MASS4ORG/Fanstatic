# Winget

The Winget manifests are generated, never hand-edited. `WingetManifest` hashes the installer produced by
`WindowsInstaller` and writes the three files a `winget-pkgs` pull request needs.

1. Build and upload the installer to a stable HTTPS URL (see [`../windows`](../windows/README.md)). The Winget
   `InstallerSha256` must match the uploaded file, so signing has to happen first.
2. Generate the manifests with the final URL:

   ```sh
   ./build.sh WingetManifest \
     --configuration Release \
     --runtime-identifier win-x64 \
     --winget-installer-url https://github.com/MASS4ORG/Fanstatic/releases/download/v<version>/Fanstatic-<version>-win-x64-setup.exe
   ```

   Output goes to `artifacts/winget/<version>`: `MASS4ORG.Fanstatic.yaml`, `MASS4ORG.Fanstatic.installer.yaml`
   and `MASS4ORG.Fanstatic.locale.en-US.yaml`, targeting Winget manifest schema `1.12.0`.
3. Validate them:

   ```sh
   winget validate --manifest artifacts/winget/<version>
   ```

4. Fork `microsoft/winget-pkgs`, copy the three files to `manifests/m/MASS4ORG/Fanstatic/<version>/` inside
   the fork, and open a pull request. Keep the submitted files in
   [`manifests/`](manifests/m/mass4org/Fanstatic) as the record of what shipped.

CI runs the same target with `--packaging-dry-run` on every commit, which generates the manifests and checks
the installer hash without needing a published URL. The dependency on the .NET 10 SDK is declared with
`PackageType: zip`, because `winget install` on a machine without the SDK cannot run the binary.