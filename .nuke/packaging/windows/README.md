# Windows packaging

The installer uses NSIS because `makensis` can create a Windows installer on Linux, macOS, or Windows.
The installer is per-user, needs no administrator rights, installs the published executable, adds the install
folder to the user `PATH` (so `fanstatic` works from any terminal) and a Start menu shortcut.
Uninstalling removes the `PATH` entry.

1. Install .NET 10 SDK and NSIS. On Debian/Ubuntu, run `sudo apt install nsis`.
2. Build the installer from any host that can publish `win-x64`:

   ```sh
   ./build.sh WindowsInstaller \
     --configuration Release \
     --runtime-identifier win-x64
   ```

3. Test the generated `artifacts/Fanstatic-<version>-win-x64-setup.exe` on Windows. A framework-dependent
   build requires the .NET 10 runtime/SDK on the target computer. Code-sign the final installer before release.
4. Upload that exact, signed installer to a stable HTTPS release URL. Signing or modifying it changes its hash.
5. Hand the installer over to Winget (see [`../winget`](../winget/README.md)).

`publish-on-tag.yml` builds the installer on every release (`Pack` depends on `WindowsInstaller`) but does not sign it, so
Windows SmartScreen warns on first run until signing is added. The `windows` job of `build-and-test.yml` installs the
generated setup.exe silently, builds a site with it and runs the uninstaller, so the installer is proven to work on
Windows on every commit.