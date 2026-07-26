nx=5; ny=5; nz=5; L=1;
X=zeros((nx+1)*(ny+1)*(nz+1),3); id=@(i,j,k) k*(nx+1)*(ny+1)+j*(nx+1)+i+1;
for k=0:nz, for j=0:ny, for i=0:nx, X(id(i,j,k),:)=[i*L/nx j*L/ny k*L/nz]; end, end, end
E=zeros(nx*ny*nz,8); e=0;
for k=0:nz-1, for j=0:ny-1, for i=0:nx-1
  e=e+1; E(e,:)=[id(i,j,k) id(i+1,j,k) id(i+1,j+1,k) id(i,j+1,k) id(i,j,k+1) id(i+1,j,k+1) id(i+1,j+1,k+1) id(i,j+1,k+1)];
end, end, end
C = X(:,3);
solidmesh(E, X, C);   % volumen completo; el corte se hace con el SLIDER interactivo
