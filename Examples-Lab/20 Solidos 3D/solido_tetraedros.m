%% Solido DISCRETIZADO en tetraedros de 4 nudos — MATLAB 2017a y Hekatan Lab
% Un bloque se malla primero con hexaedros de 8 nudos y luego CADA hexaedro
% se parte en 6 tetraedros de 4 nudos (division estandar de Freudenthal).
% Se dibuja con tetramesh(), que es MATLAB estandar.
clear all; close all;

nx = 3; ny = 2; nz = 2;
dx = 1.0; dy = 1.0; dz = 0.8;

%% 1) Nudos de la malla estructurada
nnx = nx+1; nny = ny+1; nnz = nz+1;
P = zeros(nnx*nny*nnz, 3);
id = @(i,j,k) i + (j-1)*nnx + (k-1)*nnx*nny;   % numeracion de nudo
for k = 1:nnz
  for j = 1:nny
    for i = 1:nnx
      P(id(i,j,k),:) = [(i-1)*dx, (j-1)*dy, (k-1)*dz];
    end
  end
end

%% 2) Hexaedros de 8 nudos
HEX = zeros(nx*ny*nz, 8);
e = 0;
for k = 1:nz
  for j = 1:ny
    for i = 1:nx
      e = e + 1;
      HEX(e,:) = [id(i,j,k)     id(i+1,j,k)     id(i+1,j+1,k)     id(i,j+1,k) ...
                  id(i,j,k+1)   id(i+1,j,k+1)   id(i+1,j+1,k+1)   id(i,j+1,k+1)];
    end
  end
end

%% 3) Cada hexaedro -> 6 tetraedros de 4 nudos
D = [1 2 4 5;  2 3 4 7;  2 4 5 7;  2 5 6 7;  4 5 7 8;  2 3 7 6];
TET = zeros(size(HEX,1)*6, 4);
m = 0;
for e = 1:size(HEX,1)
  for s = 1:6
    m = m + 1;
    TET(m,:) = HEX(e, D(s,:));
  end
end

%% 4) Campo escalar por tetraedro (altura del centroide)
zc = zeros(size(TET,1),1);
for m = 1:size(TET,1)
  zc(m) = mean(P(TET(m,:),3));
end

fprintf('nudos               = %d\n', size(P,1));
fprintf('hexaedros (8 nudos) = %d\n', size(HEX,1));
fprintf('tetraedros (4 nudos)= %d   (6 por hexaedro)\n', size(TET,1));
fprintf('volumen total       = %.3f m3  (esperado %.3f)\n', ...
        nx*dx*ny*dy*nz*dz, nx*dx*ny*dy*nz*dz);
fprintf('campo z centroide   = %.2f a %.2f\n', min(zc), max(zc));

figure
tetramesh(TET, P, zc);
view(3); axis equal; grid on;
xlabel('X [m]'); ylabel('Y [m]'); zlabel('Z [m]');
title('Solido discretizado en tetraedros de 4 nudos');
colorbar
