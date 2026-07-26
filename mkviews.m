coords=csvread('coords.csv'); surf=csvread('surf.csv'); Uf2=csvread('U.csv');
meta=csvread('strainloc_meta.csv'); nst=meta(1); N=meta(2); sc=12;
Uf=Uf2((nst-1)*N+1:nst*N,:); Um=sqrt(sum(Uf.^2,2)); V=coords+sc*Uf;
vw=[35 12; 90 0; 0 0]; nm={'v_iso','v_side','v_front'};
for k=1:3
  fig=figure('Position',[100 100 500 560],'Color','w'); colormap(jet);
  trisurf(surf,V(:,1),V(:,2),V(:,3),Um,'EdgeColor','none');
  axis equal; axis off; view(vw(k,1),vw(k,2)); caxis([0 3]); colorbar;
  title(sprintf('vista(%d,%d)',vw(k,1),vw(k,2)));
  print(fig,'-dpng','-r90','-opengl',[nm{k} '.png']); close(fig);
end
