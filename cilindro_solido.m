% Cilindro REAL (OpenSees strainloc) como SOLIDO macizo, coloreado por |U|. Corta con el slider
% para ver la banda de cortante RELLENA por dentro. tets=volumen, coords=deformada.
coords=csvread('cyl_coords.csv');   % nodos deformados (N x 3)
tets=csvread('cyl_tets.csv');       % tetraedros de volumen (M x 4, 1-based)
umag=csvread('cyl_umag.csv');       % |U| por nodo
solidmesh(tets, coords, umag);      % visor 3D orbit + slider de corte (relleno)
