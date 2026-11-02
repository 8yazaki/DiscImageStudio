<div align="center">

<p align="center"><a href="README.md">简体中文</a> · <b>English</b> · <a href="README.ja.md">日本語</a></p>

![Banner](docs/images/DiscImageStudio_Banner_2.png)

<h2>An all-in-one tool for generating and burning visible images on disc surfaces</h2>

Disc Image Studio is a Windows tool that generates disc-surface images and burns them, letting you etch a pattern visible to the naked eye onto **any** DVD / CD / Blu-ray disc with **any** burner drive.

![.NET version](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078d6?style=flat-square)
![Language](https://img.shields.io/badge/Language-C%23-68217A?style=flat-square)
![GitHub Repo size](https://img.shields.io/github/repo-size/JiaFeiMiao-K-Cat/DiscImageStudio?style=flat-square&color=3cb371)
![PRs welcome](https://img.shields.io/badge/PRs-Welcome-brightgreen?style=flat-square)
[![License: GPL-3.0-or-later](https://img.shields.io/badge/License-GPL--3.0--or--later-blue?style=flat-square)](LICENSE)

</div>

> This is the English translation of the [original Chinese README](README.md). If the two differ, the Chinese version is authoritative. The technical documents under `docs/` are currently available in Chinese only, and the screenshots show the Chinese UI.

---

## Results

![](docs/images/case1.jpg)

![](docs/images/case2.jpg)

---

## Overview

Disc Image Studio converts an image into data that satisfies the physical-layer constraints of CD, DVD and single-layer BD-R discs. After burning, the disc surface shows a pattern visible to the naked eye.

## UI preview

![Home: templates and recent projects](docs/images/ui-home.png)

![Design page: layer canvas and tone](docs/images/ui-design.png)

![Export page: light theme and image output](docs/images/ui-export.png)

---

## Quick start

Download the release EXE from [GitHub Releases](https://github.com/JiaFeiMiao-K-Cat/DiscImageStudio/releases), extract it and run it. The EXE already includes the .NET runtime.

### 1. Create a project

On the home page choose CD, DVD or Blu-ray, pick an output location and create the project.

### 2. Design the disc face

On the Design page import an image, arrange it with layers, and adjust dithering, gamma, brightness and gray levels in the "Tone" section of the right panel.

### 3. Preview and export

Check the disc-face projection on the Preview page. On the Export page generate ISO / RAW / WAV+CUE, or use instant burning to write straight to a blank disc.

Instant burning is experimental. Use disposable test media only.

## Features

### Disc data generation

- **CD support**: works with any CD. Export WAV tracks with a proper header plus a matching CUE sheet (compatible with cdrecord, ImgBurn and other burning software), or export RAW tracks. See [CD burning compatibility notes](docs/CD_AUDIO_COMPATIBILITY.md) (Chinese).
- **DVD support**: works with any DVD. Export an ISO image.
- **Blu-ray support**: works with any single-layer 25 GB BD-R, with pure drawing or UDF 2.01 files. See [ADDING_BLURAY.md](docs/ADDING_BLURAY.md) (Chinese).
- **Files on the disc**: for CD / DVD / Blu-ray you can pick a folder to store and choose inner-ring or outer-ring placement on the Design page, or adjust it on the Export page. The setting is saved with the project.
- **File-area preview**: the design projection and finished-disc read-back of hybrid discs mark the file area (including the file system) in yellow.
- **Media gray scheme**: CD and DVD both use two black-and-white levels; CD uses `0D / AA`, and the DVD scrambling domain uses `A5 / 92`. See [PHYSICAL_MODEL_AUDIT.md](docs/PHYSICAL_MODEL_AUDIT.md) (Chinese).
- **Gray dither tool**: a standalone program can also turn the source image into a `0/255` two-level PNG, with open / drag-and-drop input, side-by-side preview and lossless PNG export. See [usage and build notes](tools/FourLevelDither/README.md).
- **Disc presets**: ships with parameters for several brands across CD / DVD / BD. You can also contribute your own calibrated data; see the [preset maintenance notes](docs/DISC_PRESETS.md) (Chinese).

### Assisted calibration

Disc Image Studio provides a smart and precise assisted-calibration workflow. Compared with calibrating by eye, the algorithm improves the precision of the determinable physical disc data from 10<sup>-6</sup> to 10<sup>-14</sup>. Before calibration, with a constant track pitch, the radial crosshair lines are visibly bent; after regenerating and burning with the cubic track-pitch parameters from assisted calibration, the lines come much closer to straight radial lines.

| Before · constant pitch | After · cubic pitch |
| --- | --- |
| ![DVD photo before calibration: the radial crosshair is clearly bent](docs/images/verbatim-dvd-calibration-before.jpg) | ![DVD photo after calibration: the radial crosshair is much straighter](docs/images/verbatim-dvd-calibration-after.jpg) |
| `P20261004-133519.jpg` | `P20261004-135416.jpg` |

See the [assisted calibration notes](docs/CALIBRATION.md) (Chinese) for the workflow and parameters.

### Burning

- **Image file output**: export a complete ISO / RAW file and burn it manually with suitable burning software.
- **Instant streaming burn (experimental)**: CD, DVD and BD-R are generated while burning, without saving a full image; BD-R uses MMC sequential writing.
- **Never use non-blank media!**

### Application form

- Dual entry points: with no arguments the GUI opens; with arguments the unified command line runs.
- Modular: CD, DVD and Blu-ray plug in through `IOpticalDiscModule`, sharing one command catalog and task contract. See the [architecture notes](docs/ARCHITECTURE.md) (Chinese).

## Interface language

The interface is available in **简体中文 / English / 日本語**. It follows the OS display language by default; you can also choose one in "Settings → General → Interface language", and the change **takes effect after a restart**. Command-line output stays in Chinese by default; use `--lang en|ja|zh-CN` or the `DISC_IMAGE_STUDIO_LANG` environment variable to change it. See [docs/LOCALIZATION.md](docs/LOCALIZATION.md) for how translation works and how to add a language.

## System and device requirements

- Windows 10 2004 (20H1, build 19041) or later, or Windows 11.
- Building: .NET 10 SDK (`global.json` pins 10.0.x). A framework-dependent GUI you build yourself needs the .NET 10 Windows Desktop runtime. The release EXE includes the runtime, so no separate .NET install is needed.
- Instant burning needs a CD / DVD / BD burner that Windows recognizes and a blank disc; BD-R needs a single-layer blank disc that supports sequential writing.

## Build and run

```powershell
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DiscImageStudio.App --configuration Release
```

Regression checks:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- selftest
dotnet run --project tests/DiscImageStudio.ArchitectureTests --configuration Release
dotnet run --project tests/DiscImageStudio.LocalizationTests --configuration Release
python tools/check_locales.py
```

For versioning rules, the CI-built EXE, release draft checks, MSIX packaging and signing, see the [release guide](docs/RELEASING.md) (Chinese).

## Command line

With no arguments the program opens the GUI; with arguments it works as an automation tool:

| Command | Description |
| --- | --- |
| `solve` | Generate a DVD image with the fixed fast texture mapping (CW direction); black-and-white textures can use dither blending |
| `encode` | Encode a single DVD ECC block into NRZI channel levels |
| `calibrate` | Calibration projection: sample the DVD source image with the generation geometry and project it onto the disc face with the measured geometry |
| `simulate` | Read-back simulation of a generated DVD ISO using the measured radii |
| `selftest` | Run the deterministic regression self-test of the DVD engine |
| `cd-generate` | Generate WAV + CUE / RAW tracks when there are no disc files; generate a CD data-disc ISO when `--data-dir` is given |
| `cd-preview-warp` | Calibration projection: project a CD source image with the measured geometry |
| `cd-preview-track` | Render an existing WAV or RAW CD-DA track with the calibrated geometry (read-back simulation) |
| `bd-generate` | Generate a full BD-R ISO and write a `<iso>.json` sidecar |
| `bd-info` / `bd-probe` / `bd-encode` | Export BD parameters, a raw payload probe, or a single-ECC-cluster diagnostic |

The global option `--lang <zh-CN|en|ja>` selects the output language. See the corresponding documents for each command's full parameters.

## Source layout

```text
src/DiscImageStudio.Core/          Media-independent module contracts, project/preset models, task and command catalog
src/DiscImageStudio.Imaging/       Image preprocessing and dithering helpers shared by CD/DVD/BD
src/DiscImageStudio.Burning/       Windows IMAPI2 device enumeration, stream buffering and the safe-burning layer
src/DiscImageStudio.Cd/            CD-DA generation core and the CD module
src/DiscImageStudio.Dvd/           DVD engine adapter module
src/DiscImageStudio.BluRay/        BD-R geometry, 17PP/ECC, 3T/6T mapping, streaming generation and ISO
src/DiscImageStudio.Iso/           UDF 2.01 / ISO9660 writers
src/DiscImageStudio.Localization/  Dependency-free UI text localization (zh-CN / en / ja tables)
src/DiscImageStudio.App/           WPF GUI, unified command line and store assets
src/DvdImageSolver/                Isolated DVD encoding/solving engine
tests/                             Architecture and localization regression tests (no third-party test framework)
packaging/                         MSIX manifest and packaging scripts
tools/                             Standalone helper tools and asset-generation scripts
docs/                              Architecture, engine, preset, release and other documents (Chinese)
```

## Documentation

All documents below are currently written in Chinese.

| Document | Contents |
| --- | --- |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Module abstractions, layer boundaries and extension rules |
| [DVD_ENGINE.md](docs/DVD_ENGINE.md) | Conventions of the DVD NRZI image-constraint solver, the encoding chain and self-test coverage |
| [CD_AUDIO_COMPATIBILITY.md](docs/CD_AUDIO_COMPATIBILITY.md) | CD track format, byte order and ghosting troubleshooting |
| [ADDING_BLURAY.md](docs/ADDING_BLURAY.md) | Physical model, generation/burning chain and extension points of the Blu-ray module |
| [DISC_PRESETS.md](docs/DISC_PRESETS.md) | Layered JSON format and merge rules of editable disc presets |
| [CALIBRATION.md](docs/CALIBRATION.md) | Photo rectification (outlining the outermost physical edge and the center-hole edge), curve calibration and DVD cubic track-pitch fitting |
| [PHYSICAL_MODEL_AUDIT.md](docs/PHYSICAL_MODEL_AUDIT.md) | Review of the CD / DVD generation physical model |
| [VALIDATION.md](docs/VALIDATION.md) | Local validation records of builds, regressions and real-disc tests |
| [RELEASING.md](docs/RELEASING.md) | GitHub Release, MSIX packaging, signing tests and store submission |
| [LOCALIZATION.md](docs/LOCALIZATION.md) | How UI translation works and how to add or fix a language (English) |
| [CHANGELOG.md](CHANGELOG.md) | Version history |

## Reference standards

The implementation and physical model of Disc Image Studio mainly follow these specifications:

| Medium | Standard and edition | Scope used by this project |
| --- | --- | --- |
| CD-R | [ECMA-394, 1st edition, December 2010](https://ecma-international.org/publications-and-standards/standards/ecma-394/) — *Recordable Compact Disc Systems CD-R Multi-Speed* ([official PDF](https://ecma-international.org/wp-content/uploads/ECMA-394_1st_edition_december_2010.pdf)) | CD-R media, multi-speed recording and CD-DA compatibility; this project generates CD-DA tracks. |
| DVD common format / DVD-ROM | [ECMA-267, 3rd edition, April 2001](https://ecma-international.org/publications-and-standards/standards/ecma-267/) — *120 mm DVD – Read-only disk* ([official PDF](https://ecma-international.org/wp-content/uploads/ECMA-267_3rd_edition_april_2001.pdf)) | DVD data frames, ECC, EFMPlus coding and the Annex G conversion tables; media rules for recordable discs additionally follow ECMA-359. |
| DVD-R | [ECMA-359, 1st edition, December 2004](https://ecma-international.org/publications-and-standards/standards/ecma-359/) — *80 mm (1,46 Gbytes per side) and 120 mm (4,70 Gbytes per side) DVD Recordable Disk (DVD-R)* ([official PDF](https://ecma-international.org/wp-content/uploads/ECMA-359_1st_edition_december_2004.pdf)) | Geometry, sectors, error correction and coding of single-layer DVD-R; the standard covers single- and double-sided media, and double-sided does not mean dual-layer. |
| BD-R | [ISO/IEC 30190:2021, 3rd edition, January 2021](https://www.iso.org/standard/80770.html) — information interchange and storage specification for 120 mm single-layer (25.0 GB) and dual-layer (50.0 GB) recordable BD | The experimental mode uses the single-layer 25 GB track and sector layout, error correction and coding; dual-layer discs are not supported yet. |

The common CD coding also references [ECMA-130, 2nd edition (CD-ROM, 1996)](https://ecma-international.org/publications-and-standards/standards/ecma-130/).

## Burning and media safety notes

- Disc Image Studio installs no drivers or services. Instant burning uses the IMAPI2 built into Windows and refuses non-blank media.
- Instant burning has received some real-disc feedback, but coverage across devices, media and burning entry points is still limited, so it is experimental. Use disposable test media only. An interrupted burn, a power loss or a generation speed that cannot keep up can still ruin the disc!
- The visible result depends on the disc, the burner, its firmware and the write strategy.
- For hybrid folders the directory is scanned and the layout fixed before burning starts. If a source file changes size in the meantime the task stops safely, but a disc that has already started writing may still be ruined.
- BD-R sequential writing provides no spare-area remapping; after an interruption the same disc usually cannot be written again.

## Privacy and security

- Disc Image Studio does not connect to the network, upload files or collect telemetry. See the [privacy policy](PRIVACY.md) (Chinese).
- Please do not disclose security issues in public Issues; use GitHub's private reporting as described in the [security policy](SECURITY.md) (Chinese).

## Contributing

Bug reports and code contributions are welcome. See the [contributing guide](CONTRIBUTING.md) (Chinese) for environment setup, module-boundary rules, testing requirements and the Pull Request process. Translation fixes are especially appreciated; see [docs/LOCALIZATION.md](docs/LOCALIZATION.md).

## License

The project is licensed under [GNU GPL v3.0 or later](LICENSE) (`GPL-3.0-or-later`). The CD interleaving implementation contains code ported and modified from CDImage, and the whole is distributed under a compatible GPL license; when you distribute binaries or modified versions you must comply with the source-code and other requirements. Third-party fonts and components follow their own licenses; see the [license notes](LICENSE-NOTICE.md) and the [third-party notices](THIRD-PARTY-NOTICES.txt). The full text is also available offline on the app's "About" page.

## Acknowledgements

Thanks to [arduinocelentano's CDImage project](https://github.com/arduinocelentano/cdimage), which showed how to form a visible image on the disc surface by generating CD tracks. This project's CD interleaving implementation is ported from its code and further modified, and the idea of drawing on a CD surface was inspired by it. The work of CDImage's original author and contributors provided an important foundation for this project.

---

## Star History

<a href="https://www.star-history.com/?repos=jiafeimiao-k-cat%2Fdiscimagestudio&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=jiafeimiao-k-cat/discimagestudio&type=date&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=jiafeimiao-k-cat/discimagestudio&type=date&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=jiafeimiao-k-cat/discimagestudio&type=date&legend=top-left" />
 </picture>
</a>
