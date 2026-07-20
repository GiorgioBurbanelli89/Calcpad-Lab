%% Solidos 3D con patch() — igual en MATLAB 2017a y Hekatan Lab
% Malla de hexaedros (ladrillos) coloreada por un campo escalar.
% Se usa patch(Vertices,Faces,FaceVertexCData) que es MATLAB estandar:
% NO se usa solidmesh(), que solo existe en Hekatan Lab.
clear all; close all;

nx = 4; ny = 3; nz = 2;      % numero de ladrillos por direccion
dx = 1.0; dy = 1.0; dz = 0.8;

V = [];  F = [];  C = [];
for k = 0:nz-1
  for j = 0:ny-1
    for i = 0:nx-1
      x0 = i*dx; y0 = j*dy; z0 = k*dz;
      % 8 vertices del ladrillo
      v = [x0 y0 z0; x0+dx y0 z0; x0+dx y0+dy z0; x0 y0+dy z0; ...
           x0 y0 z0+dz; x0+dx y0 z0+dz; x0+dx y0+dy z0+dz; x0 y0+dy z0+dz];
      n0 = size(V,1);
      V = [V; v];
      % 6 caras del hexaedro
      f = [1 2 3 4; 5 6 7 8; 1 2 6 5; 2 3 7 6; 3 4 8 7; 4 1 5 8] + n0;
      F = [F; f];
      % color por altura del centro (campo escalar)
      val = z0 + dz/2;
      C = [C; val*ones(6,1)];
    end
  end
end

fprintf('ladrillos = %d\n', nx*ny*nz);
fprintf('vertices  = %d\n', size(V,1));
fprintf('caras     = %d\n', size(F,1));
fprintf('campo     = %.2f a %.2f\n', min(C), max(C));

figure
patch('Vertices', V, 'Faces', F, 'FaceVertexCData', C, ...
      'FaceColor', 'flat', 'EdgeColor', [0.2 0.2 0.2]);
view(3); axis equal; grid on;
xlabel('X [m]'); ylabel('Y [m]'); zlabel('Z [m]');
title('Malla de hexaedros coloreada por altura');
colorbar
