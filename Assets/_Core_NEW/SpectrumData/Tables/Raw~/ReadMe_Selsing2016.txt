J/A+A/585/A87       Quasar composite made from bright QSOs      (Selsing+, 2016)
================================================================================
An X-shooter composite of bright 1 < z < 2 quasars from UV to infrared.
    Selsing J., Fynbo J. P. U, Christensen L., Krogager J.-K.
    <Astron. Astrophys. 585, A87 (2016)>
    =2016A&A...585A..87S        (SIMBAD/NED BibCode)
================================================================================
ADC_Keywords: QSOs ; Spectra, ultraviolet ; Spectra, infrared
Keywords: quasars: general - galaxies: ISM - methods: data analysis -
          techniques: spectroscopic

Abstract:
    Quasi-stellar object (QSO) spectral templates are important both to
    QSO physics and for investigations that use QSOs as probes of
    intervening gas and dust. However, combinations of various QSO samples
    obtained at different times and with different instruments so as to
    expand a composite and to cover a wider rest frame wavelength region
    may create systematic effects, and the contribution from QSO hosts may
    contaminate the composite. We have constructed a composite spectrum
    from luminous blue QSOs at 1<z<2.1 selected from the Sloan Digital
    Sky Survey (SDSS). The observations with X-shooter simultaneously
    cover ultraviolet (UV) to near-infrared (NIR) light, which ensures
    that the composite spectrum covers the full rest-frame range from
    Ly{beta} to 11350{AA} without any significant host contamination.
    Assuming a power-law continuum for the composite we find a spectral
    slope of {alpha}{lambda}=1.70+/-0.01, which is steeper than
    previously found in the literature. We attribute the differences to
    our broader spectral wavelength coverage, which allows us to
    effectively avoid fitting any regions that are affected either by
    strong QSO emissions lines (e.g., Balmer lines and complex [FeII]
    blends) or by intrinsic host galaxy emission. Finally, we demonstrate
    the application of the QSO composite spectrum for evaluating the
    reddening in other QSOs.

Description:
    Quasar composite made from bright QSOs, as described in the paper. The
    composite consists of wavelength with corresponding arbitrarily
    normalized flux and error.
    The source code and composite is also made available at
    https://github.com/jselsing/QuasarComposite

File Summary:
--------------------------------------------------------------------------------
 FileName      Lrecl  Records   Explanations
--------------------------------------------------------------------------------
ReadMe            80        .   This file
spectrum.dat      22    25875   Quasar composite spectrum
--------------------------------------------------------------------------------

Byte-by-byte Description of file: spectrum.dat
--------------------------------------------------------------------------------
   Bytes Format Units   Label     Explanations
--------------------------------------------------------------------------------
   1-  7  F7.1  0.1nm   lambda    Wavelength array for the composite
   9- 15  F7.4  ---     S         Normalized flux density,
                                   normalized to unity at 6800{AA} (1)
  17- 22  F6.4  ---   e_S         Error relative to the normalized flux density
--------------------------------------------------------------------------------
Note (1): Constituent spectra are corrected for galactic extinction using their
           coordinates and the extinctions map from
           http://irsa.ipac.caltech.edu/applications/DUST/
--------------------------------------------------------------------------------

Acknowledgements:
      Jonatan Selsing, jselsing(at)dark-cosmology.dk

================================================================================
(End)       Jonatan Selsing [DARK, NBI], Patricia Vannier [CDS]      26-Oct-2015
