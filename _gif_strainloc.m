S=load('strainloc.mat'); coords=S.coords; surf=double(S.surf); U=S.U;
nst=size(U,1); sc=12;
Vend=coords+sc*reshape(U(end,:,:),size(coords));
mn=min(Vend)-8; mx=max(Vend)+8; lims=[mn(1) mx(1) mn(2) mx(2) mn(3) mx(3)];
fig=figure('Position',[100 100 620 640],'Color','w'); colormap(jet);
gif='cilindro_strainloc.gif';
for st=1:nst
  Uf=reshape(U(st,:,:),size(coords)); Um=sqrt(sum(Uf.^2,2)); V=coords+sc*Uf;
  cla; trisurf(surf,V(:,1),V(:,2),V(:,3),Um,'EdgeColor','none');
  axis equal; axis(lims); axis off; view(35,12); caxis([0 3]); colorbar;
  title(sprintf('Cilindro OpenSees=STKO  paso %d/%d  |U|max=%.2fmm',st,nst,max(Um)));
  drawnow; fr=getframe(fig); [A,map]=rgb2ind(frame2im(fr),256);
  if st==1, imwrite(A,map,gif,'gif','LoopCount',Inf,'DelayTime',0.12);
  else imwrite(A,map,gif,'gif','WriteMode','append','DelayTime',0.12); end
end
disp('GIF listo');
