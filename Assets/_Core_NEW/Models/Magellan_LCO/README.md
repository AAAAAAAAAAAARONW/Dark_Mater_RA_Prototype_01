# Magellan 6.5 m telescope (LCO model)

**Model by Las Campanas Observatory (LCO).** LCO asks that its authorship be shown wherever
the model is used. The credits' end card carries it (CreditsRoll_NEW, Acknowledgements):

> Magellan telescope model courtesy of Las Campanas Observatory (LCO)

Keep that line if the credits change.

## What is here

- `STL/` holds the model as LCO sent it ("Modelo Agosto 2022"): the parts are laid out for 3D printing,
  each in its own place and turn, in millimetres at full size (the primary is 6502 mm across).
- `Meshes/`, `Materials/` and `Magellan_NEW.prefab` are made from it by
  **Tools > Journey NEW > Magellan > Build Telescope from STL** (MagellanBuilder_NEW). Run it again
  after changing the STLs or the builder. It keeps the materials you have tuned.

## How the parts were put together

Measured from the STLs, not given with them:

- **Primario** (mirror, cell, centre section, tertiary tower) is already upright: the optical axis is +Y,
  and the elevation axis runs along Z through the centre section at y = 4.48 m. The mirror's vertex is at
  y = 3.705 m and its sag fits f/1.25 (focal length 8.125 m).
- **Secundario** (top ring, spider, secondary) lies on its side. It is stood up with the secondary's
  vertex 6.875 m above the primary's: the f/11 Cassegrain spacing, which puts the folded focus 4.9 m out
  along the elevation axis.
- **Espejo terciario** is a flat plate on the end of a 2.85 m support tube, square to it. Only the plate is
  used, turned to 45 degrees at the elevation axis to send the light out along it: turned that way the tube
  would run down through the primary. Where the tube really goes is not known from the STL.
- **Discos** (elevation discs) are used twice, one each side, on the axis.
- **Soporte discos EL** is used twice, under the discs.
- **Base magallanes** goes under the supports.
- **Ambos truss** is not used: its struts are drawn by the builder, eight in Serrurier pairs,
  0.22 m thick like the STL's.
