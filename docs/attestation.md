# Artifact attestation

Artifact attestation helps establish a link between the built binaries and their corresponding source code.
In the context of Windows PV Drivers for XCP-ng (WinPV for short), our artifact attestation aims to extend this link to signed binaries (WHQL-signed drivers, EV-signed guest agent, etc.)
With this, even signed binaries benefit from an attested build pipeline.

## How to verify binaries

### Step 1: Obtain attested catalog

Each release includes an attested catalog (`drivers-catalog.csv`, `guestagent-catalog.csv`).
You can find them in the Assets section of each release.

### Step 2: Verify catalog attestation

Use the GitHub CLI to verify the downloaded catalog for correspondence with its source code.
For example:

```
$ gh attestation verify drivers-catalog.csv -R xcp-ng/win-pv-drivers --deny-self-hosted-runners
Loaded digest sha256:... for file://drivers-catalog.csv
Loaded 1 attestation from GitHub API

The following policy criteria will be enforced:
- Predicate type must match:..................... https://slsa.dev/provenance/v1
- Source Repository Owner URI must match:........ https://github.com/xcp-ng
- Source Repository URI must match:.............. https://github.com/xcp-ng/win-pv-drivers
- Subject Alternative Name must match regex:..... (?i)^https://github.com/xcp-ng/win-pv-drivers/
- OIDC Issuer must match:........................ https://token.actions.githubusercontent.com
- Action workflow Runner Environment must match : github-hosted

✓ Verification succeeded!

The following 1 attestation matched the policy criteria

- Attestation #1
  - Build repo:..... xcp-ng/win-pv-drivers
  - Build workflow:. .github/workflows/build-installer.yml@...
  - Signer repo:.... xcp-ng/win-pv-drivers
  - Signer workflow: .github/workflows/attest.yml@...
```

Alternatively, you can verify the catalog by using the attestation link provided with each release.

### Step 3: Verify final binaries

Use the [artifact.psm1](/scripts/artifact.psm1) PowerShell module to verify the signed binaries:

```powershell
> Import-Module scripts\artifact.psm1 -Force

> Test-ArtifactCatalog -TrustedCatalog (Import-Csv .\drivers-catalog.csv) -ComparePath .\drivers-signed\xenbus -CatalogPrefix .\xenbus\x64\Release -Include *.sys, *.dll, *.exe, *.inf
```

**Tip**: You can verify parts of the catalog by choosing `CatalogPrefix` accordingly.

## Implementation details

WinPV's build and attestation machinery is provided by [GitHub Actions](https://docs.github.com/en/actions/security-for-github-actions/using-artifact-attestations/using-artifact-attestations-to-establish-provenance-for-builds).

Our build pipeline consists of 3 workflows:
[build-drivers](/.github/workflows/build-drivers.yml),
[build-guestagent](/.github/workflows/build-guestagent.yml) and
[build-installer](/.github/workflows/build-installer.yml).
The first two provide both unsigned (but attested) and testsigned binaries using a certificate supplied through secrets;
the latter provides a complete installer using testsigned binaries.

Here are the general build steps for each workflow:

1. Inject branding information
2. Execute build script
3. Generate catalog
4. Attest build and catalog
5. Sign build

See below for explanations of specific elements of the build steps.

### Branding information and attestation safety

In normal builds, branding is injected via [branding.ps1](/docs/build.md).
However, this route is not secure against malicious instructions injected via the branding file.

To mitigate this issue, we inject branding via a [sanitization script](/scripts/branding-ci.ps1), which verifies that branding variables follow an authorized pattern. Signing certificates are injected via a [similar script](/scripts/signer-ci.ps1).

### Catalog generation

Standard attestation protects an artifact's flat hash.
Once a binary is attested with GitHub, it cannot be modified whatsoever without breaking the attestation, including re-signing the binary with another certificate.

To extend the attestation to re-signed binaries, we use a *catalog file* (not the same as Windows `.cat` catalogs).
Catalog files are simple CSV files containing the relative path of each artifact and its Authenticode hash.
We use Authenticode hashes instead of flat hashes to avoid breaking the attestation when re-signing.
The catalog file itself is directly attested with GitHub Actions every build.

### Catalog verification

`Test-ArtifactCatalog` provides catalog filtering, prefix stripping and path normalization features.
See `artifact.psm1` for details.
