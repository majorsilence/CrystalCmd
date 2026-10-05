

[crystal report downloads](https://origin.softwaredownloads.sap.com/public/site/index.html)

- crystal 13SP39
    - https://origin-az.softwaredownloads.sap.com/public/file/0025000000164892025 
    - sha256:0716944644faf8e7c2613bd46655c79454808a2a3dd71e58642939fd7839320c
- crystal 13SP38
    - https://origin-az.softwaredownloads.sap.com/public/file/0020000000551772025
    - sha256:2f843563e1ffcb87216e7e0d1d27702b8127fd7294bcee398b9e0aa10c3412a4
- crystal 13SP37
    - https://origin-az.softwaredownloads.sap.com/public/file/0020000001375542024
    - sha256:74ed30006679aa82300468e3e58cfe014fb663e257bb35d9d3046a6e7791f4fa
    - works without needing to install `vcrun2019`

build and run
```bash
docker compose up --build -d
```

run

```bash
docker compose up
```

build oci and push to dockerhub
```bash
./build.sh
```

## The Majorsilence.Crystal worker (preview)

`Dockerfile.crystalcmd.rptengine.workeronly` builds the worker that renders with
[Majorsilence.Crystal](https://github.com/majorsilence/majorsilence.crystal) instead of the
SAP runtime. It is plain .NET 10 on Ubuntu, with no Wine, no Xvfb and no SAP install. It
builds from source, so its context is the solution folder:

```bash
docker build -f dotnet/docker/Dockerfile.crystalcmd.rptengine.workeronly -t crystalcmd-rptengine-worker dotnet/Majorsilence.CrystalCmd.NetFrameworkServer
```

It takes the work queue settings the other images take (`WorkQueue__SqlType`,
`WorkQueue__SqlConnection`) and consumes the `rptengine-reports` and `rptengine-analyzer`
channels. Only requests the API routes there reach it, so the API needs routing (newer than
release 2.0.13) and `Routing__DefaultBackend` set to `RptEngine` or `Auto`. The compose
files carry it as the `rptengine-worker` service.

It runs as the image's non-root user and writes only under `/tmp`: its work files, the
engine's unpacked fonts and fontconfig's cache. The compose service mounts the root
filesystem read-only with a tmpfs on `/tmp` and drops every capability, and the worker
renders under those settings. A health check renders a bundled sample every minute and
exits the process after repeated failures, so an orchestrator recreates it.

### Fonts

The engine's PDF writer carries its own fonts. On Linux it draws Arial, Times New Roman and
Courier New in the metric-compatible Liberation Sans, Serif and Mono, and Calibri and
Cambria in Carlito and Caladea. Text therefore wraps and paginates where it does with the
Microsoft fonts, but the glyphs differ. Verdana and Tahoma have no such substitute and are
drawn in Liberation Sans.

Fonts installed in the image are not used, so the image installs none. The worker's text
scored the same with only DejaVu installed, with the Liberation packages installed, and
with Microsoft's Arial and Verdana installed under their usual file names. Bundling the
Microsoft fonts in a published image would also raise a licensing question.

Measured with Majorsilence.Crystal's visual suite, which compares the engine's first page
with the Crystal runtime's own for each public report with a data fixture. Each score is
ink agreement in percent: the share of inked 8-pixel cells the two pages have in common.
The Linux runs are on Ubuntu 24.04, this image's base, in the en-CA culture of the machine
the references were rendered on:

| report | Linux, the engine's Liberation | Linux, real Arial | Windows, real Arial |
|---|---|---|---|
| CustomerList | 94.4 | 98.5 | 98.6 |
| SalesByCustomer-Grouped | 90.4 | 98.1 | 98.1 |
| BeforeTV | 91.4 | 95.2 | 95.2 |
| SampleReport | 91.4 | 95.8 | 96.0 |
| Country-Region-Sort | 91.3 | 94.9 | 94.9 |
| ProductPriceList | 90.5 | 92.8 | 92.8 |
| TenPct-DiscountDays | 89.2 | 91.8 | 91.8 |
| Orders10k | 87.9 | 91.1 | 92.6 |
| Orders5-150 | 87.1 | 89.7 | 91.3 |
| ProductPriceList-xs | 84.4 | 86.5 | 86.5 |

So on Linux the engine comes within 1.6 points of Windows, and Liberation in place of Arial
costs 2 to 8 points. Those points are glyph shape, not layout, since the metrics are
Arial's. For a deployment licensed to use the real Arial on Linux, the engine has to
register installed fonts by the family they declare, as it already does on Windows. That
is a change to the Reporting engine.

### Culture

Dates and numbers format in the process culture, as they do in Crystal, so the worker should
run in the culture the Crystal host formats in. The image sets `LANG=en_US.UTF-8`; set
`LANG` to change it, and leave `LC_ALL` unset, since it would override `LANG`. With real
Arial, the two reports above that print dates scored:

| report | no culture (invariant) | en_US | en_CA, the references' |
|---|---|---|---|
| Orders10k | 83.1 | 84.5 | 91.1 |
| Orders5-150 | 81.2 | 83.6 | 89.7 |
