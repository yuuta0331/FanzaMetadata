<br />
<div align="center">
  <a href="https://github.com/yuuta0331/FanzaMetadata">
    <img src="documentation/Logo.png" alt="Logo" width="256" height="256">
  </a>

  <h3 align="center">FanzaMetadata</h3>

  <p align="center">
    A Playnite metadata plugin that fetches comprehensive game metadata directly from <a href="https://www.dmm.co.jp">FANZA</a>, supporting both general (PC Games) and Doujin works!
  </p>
<br><br>

  <p align="center">
    <a href="https://github.com/yuuta0331/FanzaMetadata/releases/latest"><img src="https://img.shields.io/github/v/release/yuuta0331/FanzaMetadata" alt="Release"></a>
    <img src="https://img.shields.io/github/languages/top/yuuta0331/FanzaMetadata" alt="Language">
    <a href="https://github.com/yuuta0331/FanzaMetadata/blob/master/LICENSE"><img src="https://img.shields.io/github/license/yuuta0331/FanzaMetadata" alt="License"></a>
    <img src="https://img.shields.io/github/stars/yuuta0331/FanzaMetadata?style=social" alt="Stars">
    <br><br>
    <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/yuuta0331/FanzaMetadata/total">
    <a href="https://www.dmm.co.jp"><img src="https://img.shields.io/badge/Fanza-fa2c40" alt="Fanza"></a>
    <a href="https://www.dmm.co.jp/dc/doujin/"><img src="https://img.shields.io/badge/Fanza同人-ff8687" alt="Fanza同人"></a>

    

  </p>
</div>

---

## 🎉 Acknowledgements

This project draws inspiration and builds upon the incredible work of:
- [erri120's Playnite.Extensions](https://github.com/erri120/Playnite.Extensions)
- [Mysterken's DLsiteMetadata](https://github.com/Mysterken/DLsiteMetadata)

Special thanks to Mysterken for their outstanding DLsite metadata plugin, which laid the foundation for this project.  
FanzaMetadata introduces tailored support for FANZA while expanding its feature set.

---

## 🚀 Installation

1. Visit the [releases page](https://github.com/yuuta0331/FanzaMetadata/releases/latest) to download the latest release.
2. Install the plugin in Playnite by following its extension installation process.

---

## 📖 Usage

### Supported Metadata Fields

FanzaMetadata supports the following [fields](https://api.playnite.link/docs/api/Playnite.SDK.Plugins.MetadataField.html):

- Age rating
- Background Image
- Community Score
- Cover Image
- Description
- Developers
- Genres
- Icon
- Links
- Name
- Publishers
- Release Date
- Series

### Fetching Metadata for Both General and Doujin Works

This plugin supports fetching metadata for **general works (PC Games)** as well as **doujin works (Doujin Games)**.  
To switch between these categories, adjust the `Game Category` setting in the plugin configuration. This allows you to seamlessly fetch metadata for either type of content, based on your preferences.

### Fetching Metadata Directly from a Game Page

If the game you're searching for isn't found in the search results, you can fetch metadata directly from its game page.  
To do this, add a new link to the game with the name `Fanza` and include the URL of the game's page.

---

## ⚙️ Configuration

To configure the plugin, navigate to the plugin settings in Playnite. Below are the configurable options:

<div align="center">

| **Name**             | **Default Value** | **Description**                                                             |
|-----------------------|-------------------|-----------------------------------------------------------------------------|
| Game Category         | Doujin Games      | Choose whether to fetch metadata for general (PC Games) or doujin (Doujin Games). |
| Page Language         | Japanese          | While other languages can be selected, the plugin may not work reliably with non-Japanese settings. |
| Max Search Results    | 30                | Maximum number of search results that should appear.                        |

</div>

---

## 🌍 Important Notes

- **Page Language**: The plugin is optimized for Japanese pages. Selecting other languages may cause unexpected issues or failures in fetching metadata.
- **FANZA Restrictions**: FANZA content may be subject to specific region-based limitations. This plugin has been tested primarily within Japan.

---

## 📜 License

This project is licensed under the [MIT License](https://opensource.org/licenses/MIT).

- **Copyright (c) 2024 Mysterken**
- **Copyright (c) 2024 yuuta0331**

---

<div align="center">
  💻 **Enjoy enhanced metadata management with FanzaMetadata!** 🎮
</div>
