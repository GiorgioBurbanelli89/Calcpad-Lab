% TEST sólidos macizos en Calcpad Lab: cubo de hexaedros, CORTADO por la mitad para ver
% que el corte queda RELLENO (sin huecos). Malla nx*ny*nz de hexaedros C3D8.
nx=6; ny=6; nz=6; L=1;
% nodos
X=zeros((nx+1)*(ny+1)*(nz+1),3); id=@(i,j,k) k*(nx+1)*(ny+1)+j*(nx+1)+i+1;
for k=0:nz, for j=0:ny, for i=0:nx
  X(id(i,j,k),:)=[i*L/nx j*L/ny k*L/nz];
end, end, end
% hexaedros (orden C3D8: base 0-3, tope 4-7)
E=zeros(nx*ny*nz,8); e=0;
for k=0:nz-1, for j=0:ny-1, for i=0:nx-1
  e=e+1; E(e,:)=[id(i,j,k) id(i+1,j,k) id(i+1,j+1,k) id(i,j+1,k) ...
                 id(i,j,k+1) id(i+1,j,k+1) id(i+1,j+1,k+1) id(i,j+1,k+1)];
end, end, end
% CORTE: quedarse con los hexaedros cuyo centroide tiene y < 0.5 (mitad del cubo)
cent=zeros(size(E,1),3);
for e=1:size(E,1), cent(e,:)=mean(X(E(e,:),:),1); end
keep = cent(:,2) < 0.5*L;
Ecut = E(keep,:);
% color por altura z
C = X(:,3);
figure; solidmesh(Ecut, X, C); colormap(jet); colorbar; view(35,20);
title('Cubo hexaedros CORTADO - el corte queda RELLENO (sin huecos)');
xlabel('x'); ylabel('y'); zlabel('z');
fprintf('hexaedros totales=%d, tras corte=%d (mitad). Corte relleno con seccion interna.\n', size(E,1), size(Ecut,1));
