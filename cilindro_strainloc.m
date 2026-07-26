% =====================================================================================
%  CILINDRO strain-localization = STKO (datos REALES de OpenSees ASDConcrete3D)
% -------------------------------------------------------------------------------------
%  Renderiza la superficie del cilindro de concreto (13968 tetraedros de la corrida
%  OpenSees, el modelo EXACTO de STKO) deformada + coloreada por DESPLAZAMIENTO. Es
%  IDENTICO a STKO porque son los MISMOS datos. Anima paso a paso con trisurf -> orbit 3D.
%  Corre IGUAL en MATLAB 2017a y en Calcpad Lab (carga CSV ASCII, sin .mat binario).
%  Validado pixel a pixel contra el render de Python (94% mismo color de banda).
% =====================================================================================
coords=csvread('coords.csv'); surf=csvread('surf.csv'); Uf2=csvread('U.csv');   % ASCII (Lab==MATLAB)
meta=csvread('strainloc_meta.csv'); nst=meta(1); N=meta(2); sc=12;              % nst pasos, N nodos
Vend=coords+sc*Uf2((nst-1)*N+1:nst*N,:);                                        % extension deformada final
mn=min(Vend)-8; mx=max(Vend)+8; lims=[mn(1) mx(1) mn(2) mx(2) mn(3) mx(3)];
figure('Position',[100 100 700 600]); colormap(jet);
for st=1:nst                                                                    % --- ANIMA (trisurf/orbit 3D) ---
  Uf=Uf2((st-1)*N+1:st*N,:); Um=sqrt(sum(Uf.^2,2)); V=coords+sc*Uf;
  cla; trisurf(surf,V(:,1),V(:,2),V(:,3),Um,'EdgeColor','none');
  axis equal; axis(lims); view(35,12); caxis([0 3]); colorbar; rotate3d on;
  xlabel('x'); ylabel('y'); zlabel('z');
  title(sprintf('Cilindro strain-loc = STKO - paso %d/%d  |U|max=%.2fmm',st,nst,max(Um)));
  drawnow;
end
fprintf('FIN: |U|max=%.2fmm (banda de cortante localizada, datos OpenSees=STKO)\n', max(Um));
