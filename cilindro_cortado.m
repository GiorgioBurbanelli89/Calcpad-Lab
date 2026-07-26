% Cilindro REAL cortado a la MITAD (filtro tets con centroide y<0) -> se ve la banda de
% cortante RELLENA por dentro. Coloreado por |U|. Valida el corte del sólido.
coords=csvread('cyl_coords.csv'); tets=csvread('cyl_tets.csv'); umag=csvread('cyl_umag.csv');
cy=zeros(size(tets,1),1);
for e=1:size(tets,1), cy(e)=mean(coords(tets(e,:),2)); end   % centroide y de cada tet
keep = cy < 0;                                                % mitad del cilindro
solidmesh(tets(keep,:), coords, umag);                        % corte RELLENO (seccion interna)
