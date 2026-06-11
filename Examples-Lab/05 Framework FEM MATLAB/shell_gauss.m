function [Mxx4, Myy4, Mxy4] = shell_gauss(elm, u, x, y, E, nu, t)
% Momentos de shell por 2x2 Gauss + extrapolacion al nodo (procedimiento ETABS).
% Devuelve los 3 momentos en los 4 NODOS del Q4 (orden BL,BR,TR,TL), en vez de
% en el centroide como shell_centroid.m.
%
% Por que: ETABS evalua esfuerzos en los 4 puntos de Gauss 2x2 (xi,eta=+/-1/sqrt3)
% y EXTRAPOLA al nodo. El literal 1/sqrt(3)=0.5773502691896257 esta hardcodeado
% 15x en CsiGo2.dll (verificado con Ghidra), + sqrt(3) para la extrapolacion.
% El centroide (xi=eta=0) cae donde el twist Mxy es minimo => subestima el pico.
%
% MATLAB R2017a compatible (validacion cruzada Calcpad-Lab <-> MATLAB).
a = (max(x) - min(x)) / 2;
b = (max(y) - min(y)) / 2;
D = E * t^3 / (12 * (1 - nu^2));

g     = 1/sqrt(3);                 % 0.5773502691896257 (punto de Gauss)
xi_g  = [-g,  g,  g, -g];          % 4 puntos de Gauss, orden = orden de nodos
eta_g = [-g, -g,  g,  g];
xi_n  = [-1,  1,  1, -1];          % signos de los nodos en coords naturales
eta_n = [-1, -1,  1,  1];

tx = zeros(4,1); ty = zeros(4,1);
for k = 1:4
    gdof = elm(k);
    tx(k) = u(6*gdof-2);           % rotacion theta_x nodal
    ty(k) = u(6*gdof-1);           % rotacion theta_y nodal
end

% momentos en los 4 puntos de Gauss
Mxxg = zeros(4,1); Myyg = zeros(4,1); Mxyg = zeros(4,1);
for q = 1:4
    xi = xi_g(q); eta = eta_g(q);
    kxx = 0; kyy = 0; kxy = 0;
    for k = 1:4
        dNdx = 0.25 * xi_n(k)  * (1 + eta*eta_n(k)) / a;
        dNdy = 0.25 * eta_n(k) * (1 + xi *xi_n(k))  / b;
        kxx = kxx - dNdx*ty(k);
        kyy = kyy + dNdy*tx(k);
        kxy = kxy + dNdx*tx(k) - dNdy*ty(k);
    end
    Mxxg(q) = D * (kxx + nu*kyy);
    Myyg(q) = D * (nu*kxx + kyy);
    Mxyg(q) = D * (1-nu)/2 * kxy;
end

% matriz de extrapolacion Gauss -> nodo (Q4, factor 1 +/- sqrt(3)/2)
s3 = sqrt(3);
A = [1+s3/2, -0.5,   1-s3/2, -0.5;
     -0.5,    1+s3/2, -0.5,   1-s3/2;
     1-s3/2, -0.5,    1+s3/2, -0.5;
     -0.5,    1-s3/2, -0.5,   1+s3/2];

Mxx4 = (A * Mxxg)';
Myy4 = (A * Myyg)';
Mxy4 = (A * Mxyg)';
end
