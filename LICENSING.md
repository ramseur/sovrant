# Licensing

Sovrant is **source-available** under the [Business Source License 1.1](LICENSE) (BSL 1.1). The full license text is in the [`LICENSE`](LICENSE) file at the root of this repository; the canonical template is published by MariaDB at <https://mariadb.com/bsl11/>.

This document explains the choice in plain English. It is not a substitute for the LICENSE file.

## Quick reference

| | |
|---|---|
| Licensor | Anant Corporation |
| Licensed Work | Sovrant |
| Change Date | 2029-05-10 |
| Change License | Apache License, Version 2.0 |
| Initial release | 2026-05-10 |

After the Change Date, each version of the Licensed Work covered by this license automatically converts to Apache 2.0.

## What you can do today

- Read, study, and modify the source.
- Run Sovrant for your own internal use, including production.
- Run Sovrant for your customers as part of a service you provide them (consulting, professional services, integration work, white-glove implementations, etc.).
- Distribute Sovrant or modified versions of it, provided you keep the LICENSE file and notices intact.
- Contribute back through pull requests.

## What you cannot do today

- You cannot offer Sovrant or a modified version of Sovrant to third parties as a hosted, managed, or "as-a-service" product whose primary value to those third parties comes from Sovrant's features and functionality. In short: **you cannot wrap Sovrant in a website and sell it as a competing SaaS.**

That is the only commercial restriction. Everything else is permitted.

This restriction exists because Anant Corporation operates a hosted Sovrant cloud and the BSL prevents a cloud provider or competitor from taking the source and offering an identical service.

## The Change Date

Each version of Sovrant covered by this LICENSE automatically converts to Apache 2.0 on the Change Date listed in the LICENSE file at the time that version was released. After the Change Date, the SaaS-resale restriction disappears for that version. Sovrant under Apache 2.0 may be used for any purpose without exception.

When Anant Corporation cuts new releases, the LICENSE file's Change Date moves forward for those new releases, but earlier-released code keeps its earlier Change Date. The result is a rolling three-year window: the latest version is always BSL, older versions become Apache 2.0 over time.

## Why not "open source"?

The Open Source Initiative reserves the term "open source" for licenses that meet the Open Source Definition, which BSL does not. Sovrant is correctly described as **source-available** or **fair-source**, never "open source," in marketing, documentation, or community materials. This distinction matters for trademark and accuracy reasons, and Anant Corporation asks contributors and users to honor it.

Practical consequences:

- Sovrant is not eligible for inclusion in Linux distribution main repositories (Debian main, Fedora, etc.). Community packaging through Homebrew, Chocolatey, container registries, and direct downloads remains fine.
- Sovrant is not eligible for CNCF graduation or similar foundation hosting that requires OSI-approved licenses.
- Some package manager UIs may display Sovrant's license as "Other" or "Non-standard" rather than naming it.

## Contributing

By submitting a pull request, you agree that your contribution may be distributed under this LICENSE and any future license Anant Corporation releases Sovrant under. A formal Contributor License Agreement (CLA) may be required before contributions are merged once the project formalizes its governance.

## Need a different license?

If your intended use does not fit within the BSL terms — for example, if you want to build a hosted Sovrant service, embed Sovrant in a commercial product where the BSL terms are not workable, or need different commercial terms — contact Anant Corporation to discuss a separate commercial license.

## Why BSL, briefly

Sovrant chose BSL 1.1 with a three-year Apache 2.0 conversion to balance three goals: maximum adoption by businesses (BSL is widely understood by enterprise legal teams thanks to its use by MariaDB, CockroachDB, Sentry, and HashiCorp), protection for Anant Corporation's hosted offering, and a clean automatic path to a permissive license over time. Permissive licenses (Apache 2.0, MIT) were ruled out because they offer no protection for the hosted offering. AGPL was ruled out because enterprise legal departments commonly block it on contact, which would defeat the adoption goal. Newer source-available licenses (FSL, SSPL, Elastic License) were considered but rejected in favor of BSL's deeper enterprise familiarity.
