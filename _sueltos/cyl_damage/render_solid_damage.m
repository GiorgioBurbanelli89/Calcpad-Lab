% Render del cilindro SOLID DAMAGE (dato real OpenSees ASDConcrete3D) - camara canonica
coords=csvread('coords.csv'); surf=csvread('surf.csv');
Df=csvread('damage_face.csv'); U=csvread('U.csv'); meta=csvread('meta.csv');
nst=meta(1); N=meta(2); st=nst;                    % paso final
Uf=U((st-1)*N+1:st*N,:); sc=15; V=coords+sc*Uf;    % deformada
dface=Df(st,:)';                                    % dano por cara (paso final)
figure('Position',[100 100 700 600]);
patch('Faces',surf,'Vertices',V,'FaceVertexCData',dface,'FaceColor','flat','EdgeColor','none');
axis equal; view(35,12); caxis([0 1]); colormap(jet_r); cb=colorbar; cb.Label.String='dano d';
title(sprintf('Solid Damage - cilindro ASDConcrete3D (OpenSees=STKO) - paso %d/%d  d_{max}=%.3f',st,nst,max(dface)));
xlabel('x'); ylabel('y'); zlabel('z');
