%% Malla Delaunay restringida (CDT) + isInterior — igual que GEO5 / MATLAB
%  Contorno en L (no convexo) con puntos interiores. El motor Hekatan Lab
%  triangula, recupera las aristas del contorno y descarta el exterior.
clc; clear;

% --- contorno en L y sus aristas restringidas ---
V = [0 0; 4 0; 4 2; 2 2; 2 4; 0 4];          % area exacta = 12
C = [1 2; 2 3; 3 4; 4 5; 5 6; 6 1];

% --- puntos interiores en grilla (dentro de la L) ---
px = []; py = [];
for x = 0.5:0.5:3.5
  for y = 0.5:0.5:3.5
    if (x <= 2) || (y <= 2)
      px = [px; x]; py = [py; y];
    end
  end
end
P = [ [V(:,1); px] , [V(:,2); py] ];

% --- triangulacion restringida ---
DT     = delaunayTriangulation(P, C);
inside = isInterior(DT);
tri    = DT.ConnectivityList(inside, :);

% --- area total (comprobacion) ---
A = 0;
for k = 1:size(tri,1)
  n = tri(k,:); x = P(n,1); y = P(n,2);
  A = A + 0.5*abs((x(2)-x(1))*(y(3)-y(1)) - (x(3)-x(1))*(y(2)-y(1)));
end
fprintf('Triangulos interiores = %d\n', size(tri,1));
fprintf('Area de la malla       = %.4f  (exacta = 12)\n', A);

% --- dibujo ---
figure; hold on;
triplot(tri, P(:,1), P(:,2), 'b');
plot([V(:,1); V(1,1)], [V(:,2); V(1,2)], 'r-', 'LineWidth', 2);
plot(P(:,1), P(:,2), 'k.', 'MarkerSize', 10);
axis equal; grid on;
title(sprintf('Delaunay CDT: %d triangulos, area %.2f', size(tri,1), A));
xlabel('x'); ylabel('y');
