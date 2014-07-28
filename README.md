# PSUtils Launcher

This is PSUtils Launcher, a helper for using my other project: [PSUtils](https://github.com/ecsousa/PSUtils).

When started, it will download PSUtils repository from GitHub (if not exists). If ConEmu directory does not exist, will ask you whether you want to download it.

After this initialization, it will launch PowerShell importing PSUtils module, and use ConEmu if it is downloaded.

Also, if PSUtils repository already exists, it will pull to keep module up to date (can be disabled via configuration).

It does not require Git installed. Libgit2Sharp is included.

Hope you will enjoy!
