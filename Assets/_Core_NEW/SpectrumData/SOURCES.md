# Data & Sources — Journey Spectrum

Every number the bake uses, where it comes from, and how far to trust it. The values themselves live in `Tables/*.csv`. This file explains them.

> **The references below were compiled without library access.** Journal volumes and pages should be confirmed before anything is cited publicly (e.g. on an exhibit label).

## Confidence levels

| Level | Meaning |
|---|---|
| **Exact** | Physical constant or definition. Not in question |
| **Published** | Taken from a published parameter set or fit formula. The formula and coefficients are standard |
| **Approx** | Entered from memory of the paper. **Right to within tens of percent, but must be checked against the paper before being shown as data** |
| **Extrapolated** | Beyond the range any measurement covers. Chosen so it cannot change the picture |
| **Model choice** | A modelling parameter. Affects texture, not the conclusions |

---

## Cosmology — `Tables/cosmology.csv`

| Quantity | Value | Source | Confidence |
|---|---|---|---|
| H₀ | 67.66 km/s/Mpc | Planck Collaboration 2020 (Planck 2018 results VI), as in `astropy.cosmology.Planck18` | Published |
| Ωm | 0.30966 | same | Published |
| Flat universe, radiation ignored | — | Radiation is < 0.3% of H(z) at z = 8 | Model choice |

**Checks (from the bake report):** age of the universe comes out at 13.81 Gyr, consistent with Planck's 13.79 Gyr. The difference is the ignored radiation and neutrinos.

## Journey start — `bake_settings.csv: lookback_start_gyr`

| Quantity | Value | Source | Confidence |
|---|---|---|---|
| Light-travel time of the journey | 12.07 Gyr | Chosen so the quasar sits at Q1422+2309's redshift (decision 2026-09-27, see `VO_CHANGES.md`). `UniverseJourneyTracker` first phase must be 12,070,000,000 ly | Project decision |
| → quasar redshift | z = 3.62 | derived from the cosmology | Exact given the above |

**If the tracker's starting distance changes, change this setting and re-bake.** Otherwise the spectrum and the journey disagree about where the light is.

## Quasar emission — `Tables/emission_lines.csv`, `bake_settings.csv`

| Quantity | Value | Source | Confidence |
|---|---|---|---|
| Continuum slope above 912 Å | F_λ ∝ λ^−1.70 | Selsing et al. 2016, A&A 585, A87 (X-shooter composite) | Published |
| Continuum slope below 912 Å | F_λ ∝ λ^−0.3 (α_ν = −1.7) | Lusso et al. 2015, MNRAS 449, 4204 | Published |
| Emission-line equivalent widths | see CSV | Vanden Berk et al. 2001, AJ 122, 549, Table 2 | **Approx — verify** |
| Emission-line widths | 3500–5000 km/s (broad), 1000 (narrow [O III]) | typical values | Model choice |

**Upgrade path:** replace the Gaussian template with the actual Selsing et al. (2016) composite spectrum (public, 1000–11000 Å rest frame).

## Intergalactic absorption

### Mean transmission — `Tables/mean_flux.csv`

The bake calibrates its absorption so that the model's average transmission at each z equals these values. See *Calibration check* below.

| z range | Source | Confidence |
|---|---|---|
| 2.0–4.5 | Becker et al. 2013, MNRAS 430, 2067. Fit: τ_eff = 0.751((1+z)/4.5)^2.90 − 0.132 | Published (computed from the fit) |
| 5.0–6.0 | Bosman et al. 2022, MNRAS 514, 55; Eilers et al. 2018, ApJ 864, 53 | **Approx — verify** |
| 6.2–8.0 | Gunn–Peterson trough | Extrapolated |
| 0–1 | HST low-z forest, e.g. Danforth et al. 2016, ApJ 817, 111 | **Approx — verify** (does not affect the display) |

### Absorber texture — `bake_settings.csv`

| Quantity | Value | Source | Confidence |
|---|---|---|---|
| Method | Fluctuating Gunn–Peterson approximation, τ = A(z)·ρ^α on a lognormal density field | Standard (e.g. Weinberg et al. 1997; Bi & Davidsen 1997) | Published method |
| α | 1.6 | α = 2 − 0.7(γ − 1), γ ≈ 1.6 (Hui & Gnedin 1997) | Published |
| Density contrast σ | 1.0 | — | Model choice |
| Correlation length | 100 km/s | order of the Jeans scale | Model choice |
| Lyβ / Lyα optical depth | 0.1603 | (f λ)_β / (f λ)_α, atomic oscillator strengths | Exact |

**The individual absorbers are generated, not observed.** Their statistics (the mean absorption at each z) match measurements. Their exact positions do not correspond to any real sightline.

### Ionising absorption below 912 Å — `Tables/mean_free_path.csv`

| z range | Source | Confidence |
|---|---|---|
| 2–5 | Worseck et al. 2014, MNRAS 445, 1745. λ_mfp = 37((1+z)/5)^−5.4 h₇₀⁻¹ pMpc | Published fit; **values approx — verify** |
| 5.1, 6.0 | Becker et al. 2021, MNRAS 508, 1853 | **Approx — verify** |
| > 6 | held at the z = 6 value | Extrapolated. This **under**states the real opacity |
| Wavelength dependence | (λ/912)^1.5 | follows from a column-density distribution f(N) ∝ N^−1.5 | Model choice |

### Helium — `bake_settings.csv: heii_reionization_z`

| Quantity | Value | Source | Confidence |
|---|---|---|---|
| He II edge | 227.84 Å | atomic | Exact |
| He II fully ionised below | z = 3.0 | e.g. Worseck et al. 2011, 2016 | Published (approximate epoch) |
| Treatment | opaque below 228 Å locally at z > 3 | — | Model choice |

### Proximity zone — `bake_settings.csv: proximity_zone_pmpc`

| Quantity | Value | Source | Confidence |
|---|---|---|---|
| Zone size (transmission → 10%) | 1.5 proper Mpc | Measured for z > 7 quasars: J1120+0641 ≈ 1.9 (Mortlock et al. 2011), J1342+0928 ≈ 1.3 (Bañados et al. 2018); see Eilers et al. 2017 | **Approx — verify** |

Only matters when the surrounding gas transmits less than 10%, i.e. z ≳ 5. At the chosen z = 3.62 the report shows R_ion = 0: no proximity boost is applied. The real, milder z ≈ 3 proximity effect is not modelled.

---

## Real spectra for validation (not used yet)

| Object | z | Reference |
|---|---|---|
| ULAS J1120+0641 | 7.08 | Mortlock et al. 2011, Nature 474, 616 |
| ULAS J1342+0928 | 7.54 | Bañados et al. 2018, Nature 553, 473 |
| DES J0313−1806 | 7.64 | Wang et al. 2021, ApJL 907, L1 |
| XQR-30 sample (~30 quasars, z ≈ 6) | 5.8–6.6 | D'Odorico et al. 2023, MNRAS 523, 1399 (public X-shooter spectra) |

The bake's last row should resemble these. See `OPEN_QUESTIONS.md` §5.

---

## Calibration check

Every bake writes `Baked/SpectrumJourney_report.txt`. Its **CALIBRATION** table shows the model reproducing each mean-transmission value exactly. Its **PATH CHECK** table shows what was actually imprinted along the path, which is noisier at low z, where few journey steps fall. Read these after changing any table.
