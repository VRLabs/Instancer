<div align="center">

# Instancer

[![Generic badge](https://img.shields.io/github/downloads/VRLabs/Instancer/total?label=Downloads)](https://github.com/VRLabs/Instancer/releases/latest)
[![Generic badge](https://img.shields.io/badge/License-MIT-informational.svg)](https://github.com/VRLabs/Instancer/blob/main/LICENSE)
[![Generic badge](https://img.shields.io/badge/Unity-2019.4.31f1-lightblue.svg)](https://unity3d.com/unity/whats-new/2019.4.31)
[![Generic badge](https://img.shields.io/badge/SDK-AvatarSDK3-lightblue.svg)](https://vrchat.com/home/download)

[![Generic badge](https://img.shields.io/discord/706913824607043605?color=%237289da&label=DISCORD&logo=Discord&style=for-the-badge)](https://discord.vrlabs.dev/)
[![Generic badge](https://img.shields.io/endpoint.svg?url=https%3A%2F%2Fshieldsio-patreon.vercel.app%2Fapi%3Fusername%3Dvrlabs%26type%3Dpatrons&style=for-the-badge)](https://patreon.vrlabs.dev/)

VRLabs' Instancing system that copies files over for use in the Assets directory and can also runs install scripts

### ⬇️ [Download Latest Version](https://github.com/VRLabs/Instancer/releases/latest)

### 📦 [Add to VRChat Creator Companion](https://vrlabs.dev/packages?package=dev.vrlabs.instancer)

</div>

---

## How it works

* It gets called by a small dummy file included in the package, and when called copies all the files over, and replaces references to old files with references to new files.
* It can also run an install script callback once it's done with this.

## Install guide

* It should generally only be installed as a dependency of a VRLabs package

## How to use

* Click the `VRLabs/[PackageName]` button in the toolbar and select an output folder to copy to.

### Instance Any Package

* You can use the `VRLabs/Instance Any Package` button to instance and rename any package in the Assets folder.
* It will ask you for the package name and the new instance name.
  * Note that it will only rename things that start with the package name, so if you have e.g. controller parameters that start with a different name, they wont be renamed and will clash with the initial package.
  * Note that materials will reference shaders from the original package as shaders are not copied over.
  * Note that big packages might crash your Unity Editor and cancel the renames, so notall packages will work.

## Contributors

* [Jelle](https://jellejurre.dev)

## License

Instancer is available as-is under MIT. For more information see [LICENSE](https://github.com/VRLabs/Instancer/blob/main/LICENSE).

​

<div align="center">

[<img src="https://github.com/VRLabs/Resources/raw/main/Icons/VRLabs.png" width="50" height="50">](https://vrlabs.dev "VRLabs")
<img src="https://github.com/VRLabs/Resources/raw/main/Icons/Empty.png" width="10">
[<img src="https://github.com/VRLabs/Resources/raw/main/Icons/Discord.png" width="50" height="50">](https://discord.vrlabs.dev/ "VRLabs")
<img src="https://github.com/VRLabs/Resources/raw/main/Icons/Empty.png" width="10">
[<img src="https://github.com/VRLabs/Resources/raw/main/Icons/Patreon.png" width="50" height="50">](https://patreon.vrlabs.dev/ "VRLabs")
<img src="https://github.com/VRLabs/Resources/raw/main/Icons/Empty.png" width="10">
[<img src="https://github.com/VRLabs/Resources/raw/main/Icons/Twitter.png" width="50" height="50">](https://twitter.com/vrlabsdev "VRLabs")

</div>

