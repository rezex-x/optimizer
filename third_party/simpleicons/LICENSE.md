# Simple Icons — vendored brand mark data

This directory contains SVG files vendored from the [Simple Icons](https://simpleicons.org)
project ([github.com/simple-icons/simple-icons](https://github.com/simple-icons/simple-icons)),
fetched from `https://raw.githubusercontent.com/simple-icons/simple-icons/master/icons/<slug>.svg`
at the commit current as of 2026-09-19. Only the single `<path d="…">` glyph data from each SVG
is used by the app (see `src/Tweaker/Resources/BrandIcons.cs`); the `.svg` files here are kept as
the offline-vendored source of that data.

## License

Simple Icons' icon data (the SVG path shapes themselves) is released under the **CC0 1.0
Universal** public domain dedication, as declared in the project's own `package.json`
(`"license": "CC0-1.0"`) and `LICENSE.md`. The full CC0 1.0 Universal legal text, as published
by the Simple Icons project, is reproduced below.

## Trademark notice

Although the icon shapes are released under CC0, the **brand names, logos, and marks they
depict remain the trademarks of their respective owners** (Google, Mozilla, Brave Software,
LibreWolf, Vivaldi Technologies, the Git project, OpenJS Foundation / Node.js, the Python
Software Foundation, Docker Inc., Postman Inc., Igor Pavlov / 7-Zip, VideoLAN, Notepad++ / Don
Ho, OBS Project, the GIMP project, Spotify AB, Valve Corporation, Epic Games Inc., Discord Inc.,
Malwarebytes Inc., and the KeePassXC team). These marks are used here strictly for **nominative,
identifying purposes** — i.e. to let a user visually recognize the corresponding app in the
Tweaker Install tab's app list — and no endorsement, sponsorship, or affiliation with Tweaker is
claimed or implied.

## Vendored files

Each `<slug>.svg` below corresponds to one winget app id in
`src/Tweaker.Core/Catalog/AppCatalog.cs`, mapped via `BrandIcons.Paths` in
`src/Tweaker/Resources/BrandIcons.cs`:

| file | winget id |
|---|---|
| `googlechrome.svg` | `Google.Chrome` |
| `firefoxbrowser.svg` | `Mozilla.Firefox` |
| `brave.svg` | `Brave.Brave` |
| `librewolf.svg` | `LibreWolf.LibreWolf` |
| `vivaldi.svg` | `Vivaldi.Vivaldi` |
| `git.svg` | `Git.Git` |
| `nodedotjs.svg` | `OpenJS.NodeJS.LTS` |
| `python.svg` | `Python.Python.3.12` |
| `docker.svg` | `Docker.DockerDesktop` |
| `postman.svg` | `Postman.Postman` |
| `7zip.svg` | `7zip.7zip` |
| `vlcmediaplayer.svg` | `VideoLAN.VLC` |
| `notepadplusplus.svg` | `Notepad++.Notepad++` |
| `obsstudio.svg` | `OBSProject.OBSStudio` |
| `gimp.svg` | `GIMP.GIMP` |
| `spotify.svg` | `Spotify.Spotify` |
| `steam.svg` | `Valve.Steam` |
| `epicgames.svg` | `EpicGames.EpicGamesLauncher` |
| `discord.svg` | `Discord.Discord` |
| `malwarebytes.svg` | `Malwarebytes.Malwarebytes` |
| `keepassxc.svg` | `DominikReichl.KeePass` |

Note: `DominikReichl.KeePass` (KeePass) uses the Simple Icons `keepassxc` glyph (KeePassXC), a
community fork with a very similar mark, since KeePass itself does not have a dedicated Simple
Icons entry; this mapping was specified by the task brief.

`handbrake` was requested but does not exist in Simple Icons (confirmed 404 and absent from the
project's `data/simple-icons.json`), so `HandBrake.HandBrake` was skipped and keeps its monogram
tile. The following apps were intentionally never fetched and also keep their monogram tile per
the task brief: `Microsoft.Edge`, `Microsoft.VisualStudioCode`, `Microsoft.WindowsTerminal`,
`Microsoft.WSL`, `voidtools.Everything`, `TGRMNSoftware.BulkRenameUtility`,
`Microsoft.PowerToys`, `IrfanView.IrfanView`, `Guru3D.Afterburner`, `IDRIX.VeraCrypt`,
`Henry++.simplewall`.

---

# CC0 1.0 Universal

## Statement of Purpose

The laws of most jurisdictions throughout the world automatically confer exclusive Copyright and
Related Rights (defined below) upon the creator and subsequent owner(s) (each and all, an
"owner") of an original work of authorship and/or a database (each, a "Work").

Certain owners wish to permanently relinquish those rights to a Work for the purpose of
contributing to a commons of creative, cultural and scientific works ("Commons") that the public
can reliably and without fear of later claims of infringement build upon, modify, incorporate in
other works, reuse and redistribute as freely as possible in any form whatsoever and for any
purposes, including without limitation commercial purposes. These owners may contribute to the
Commons to promote the ideal of a free culture and the further production of creative, cultural
and scientific works, or to gain reputation or greater distribution for their Work in part
through the use and efforts of others.

For these and/or other purposes and motivations, and without any expectation of additional
consideration or compensation, the person associating CC0 with a Work (the "Affirmer"), to the
extent that he or she is an owner of Copyright and Related Rights in the Work, voluntarily elects
to apply CC0 to the Work and publicly distribute the Work under its terms, with knowledge of his
or her Copyright and Related Rights in the Work and the meaning and intended legal effect of CC0
on those rights.

1. **Copyright and Related Rights.** A Work made available under CC0 may be protected by
   copyright and related or neighboring rights ("Copyright and Related Rights"). Copyright and
   Related Rights include, but are not limited to, the following:
    1. the right to reproduce, adapt, distribute, perform, display, communicate, and translate a
       Work;
    2. moral rights retained by the original author(s) and/or performer(s);
    3. publicity and privacy rights pertaining to a person's image or likeness depicted in a
       Work;
    4. rights protecting against unfair competition in regards to a Work, subject to the
       limitations in paragraph 4(i), below;
    5. rights protecting the extraction, dissemination, use and reuse of data in a Work;
    6. database rights (such as those arising under Directive 96/9/EC of the European Parliament
       and of the Council of 11 March 1996 on the legal protection of databases, and under any
       national implementation thereof, including any amended or successor version of such
       directive); and
    7. other similar, equivalent or corresponding rights throughout the world based on
       applicable law or treaty, and any national implementations thereof.

2. **Waiver.** To the greatest extent permitted by, but not in contravention of, applicable law,
   Affirmer hereby overtly, fully, permanently, irrevocably and unconditionally waives,
   abandons, and surrenders all of Affirmer's Copyright and Related Rights and associated claims
   and causes of action, whether now known or unknown (including existing as well as future
   claims and causes of action), in the Work (i) in all territories worldwide, (ii) for the
   maximum duration provided by applicable law or treaty (including future time extensions),
   (iii) in any current or future medium and for any number of copies, and (iv) for any purpose
   whatsoever, including without limitation commercial, advertising or promotional purposes (the
   "Waiver"). Affirmer makes the Waiver for the benefit of each member of the public at large and
   to the detriment of Affirmer's heirs and successors, fully intending that such Waiver shall
   not be subject to revocation, rescission, cancellation, termination, or any other legal or
   equitable action to disrupt the quiet enjoyment of the Work by the public as contemplated by
   Affirmer's express Statement of Purpose.

3. **Public License Fallback.** Should any part of the Waiver for any reason be judged legally
   invalid or ineffective under applicable law, then the Waiver shall be preserved to the maximum
   extent permitted taking into account Affirmer's express Statement of Purpose. In addition, to
   the extent the Waiver is so judged Affirmer hereby grants to each affected person a
   royalty-free, non transferable, non sublicensable, non exclusive, irrevocable and
   unconditional license to exercise Affirmer's Copyright and Related Rights in the Work (i) in
   all territories worldwide, (ii) for the maximum duration provided by applicable law or treaty
   (including future time extensions), (iii) in any current or future medium and for any number
   of copies, and (iv) for any purpose whatsoever, including without limitation commercial,
   advertising or promotional purposes (the "License"). The License shall be deemed effective as
   of the date CC0 was applied by Affirmer to the Work. Should any part of the License for any
   reason be judged legally invalid or ineffective under applicable law, such partial invalidity
   or ineffectiveness shall not invalidate the remainder of the License, and in such case
   Affirmer hereby affirms that he or she will not (i) exercise any of his or her remaining
   Copyright and Related Rights in the Work or (ii) assert any associated claims and causes of
   action with respect to the Work, in either case contrary to Affirmer's express Statement of
   Purpose.

4. **Limitations and Disclaimers.**
    1. No trademark or patent rights held by Affirmer are waived, abandoned, surrendered,
       licensed or otherwise affected by this document.
    2. Affirmer offers the Work as-is and makes no representations or warranties of any kind
       concerning the Work, express, implied, statutory or otherwise, including without
       limitation warranties of title, merchantability, fitness for a particular purpose, non
       infringement, or the absence of latent or other defects, accuracy, or the present or
       absence of errors, whether or not discoverable, all to the greatest extent permissible
       under applicable law.
    3. Affirmer disclaims responsibility for clearing rights of other persons that may apply to
       the Work or any use thereof, including without limitation any person's Copyright and
       Related Rights in the Work. Further, Affirmer disclaims responsibility for obtaining any
       necessary consents, permissions or other rights required for any use of the Work.
    4. Affirmer understands and acknowledges that Creative Commons is not a party to this
       document and has no duty or obligation with respect to this CC0 or use of the Work.

For more information, please see <https://creativecommons.org/publicdomain/zero/1.0>.

Source: <https://github.com/simple-icons/simple-icons/blob/master/LICENSE.md>
