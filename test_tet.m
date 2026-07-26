% Cubo TETRAEDROS coloreado por z. Ahora usa ndgrid 3D (arreglado). Corre en MATLAB 2017a y Lab.
n=3; L=1;
[Xg,Yg,Zg]=ndgrid(0:L/n:L, 0:L/n:L, 0:L/n:L);    % <-- ndgrid 3D (antes fallaba en Lab)
X=[Xg(:) Yg(:) Zg(:)];
id=@(i,j,k) i + j*(n+1) + k*(n+1)*(n+1) + 1;
loc=[0 1 2 6; 0 2 3 6; 0 3 7 6; 0 7 4 6; 0 4 5 6; 0 5 1 6];
corn=[0 0 0;1 0 0;1 1 0;0 1 0;0 0 1;1 0 1;1 1 1;0 1 1];
T=zeros(6*n*n*n,4); e=0;
for k=0:n-1, for j=0:n-1, for i=0:n-1
  gi=zeros(1,8);
  for c=1:8, gi(c)=id(i+corn(c,1), j+corn(c,2), k+corn(c,3)); end
  for t=1:6, e=e+1; T(e,:)=gi(loc(t,:)+1); end
end, end, end
C=zeros(size(T,1),1);
for t=1:size(T,1), C(t)=mean(X(T(t,:),3)); end
figure; tetramesh(T, X, C, 'EdgeColor','none'); colormap(jet); caxis([0 1]);
axis equal; view(35,20); axis off; colorbar;
title('Cubo tetraedros ndgrid 3D (tetramesh)');
