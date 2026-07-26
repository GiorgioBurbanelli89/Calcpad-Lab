coords=csvread('coords.csv'); surf=csvread('surf.csv'); Uf2=csvread('U.csv');
meta=csvread('strainloc_meta.csv'); nst=meta(1); N=meta(2); sc=12;
Vend=coords+sc*Uf2((nst-1)*N+1:nst*N,:); mn=min(Vend)-8; mx=max(Vend)+8;
lims=[mn(1) mx(1) mn(2) mx(2) mn(3) mx(3)];
fig=figure('Position',[100 100 620 640],'Color','w'); colormap(jet);
for st=1:nst
  Uf=Uf2((st-1)*N+1:st*N,:); Um=sqrt(sum(Uf.^2,2)); V=coords+sc*Uf;
  cla; trisurf(surf,V(:,1),V(:,2),V(:,3),Um,'EdgeColor','none');
  axis equal; axis(lims); axis off; view(35,12); caxis([0 3]); colorbar;
  title(sprintf('Cilindro OpenSees=STKO  paso %d/%d  |U|max=%.2fmm',st,nst,max(Um)));
  print(fig,'-dpng','-r80','-opengl',sprintf('_frames/f%02d.png',st));
end
