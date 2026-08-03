%% Colormap de DESPLAZAMIENTOS de GEO5 (extraido del binario GeoFEM_5.dll)
%-- 8 anclas RGB, interpolacion lineal -> misma escala azul->rojo de GEO5.
%-- Corre en MATLAB 2017a y Hekatan Lab.

% Campo de desplazamiento de ejemplo (|d| maximo al centro)
[X,Y] = meshgrid(linspace(0,6,90), linspace(0,4,90));
d = 10*exp(-((X-3).^2/4 + (Y-2).^2/2));

figure;
contourf(X, Y, d, 20, 'LineStyle', 'none'); colorbar;
colormap(geo5disp);
title('Desplazamiento |d| [mm] - colormap GEO5'); xlabel('x [m]'); ylabel('y [m]');

%% ---- Colormap GEO5 desplazamientos (8 anclas del binario) ----
function cmap = geo5disp(N)
    if nargin < 1, N = 256; end
    A = [  0   0 255;      % azul
           0   0 176;      % navy
           0 176   0;      % verde oscuro
           0 255   0;      % verde
         255 255   0;      % amarillo
         255 128   0;      % naranja
         255   0   0;      % rojo
         176   0   0] / 255;  % rojo oscuro
    na = size(A,1);
    xi = linspace(1, na, N);
    cmap = [interp1(1:na, A(:,1), xi)', interp1(1:na, A(:,2), xi)', interp1(1:na, A(:,3), xi)'];
end
