function T = tframe(n0, n1)
% Transformacion frame 3D 12x12 (convencion Z-up).
% Port hekatan_fem/utils/transformation.py::frame
v = n1 - n0;
L = norm(v);
l = v(1)/L; m = v(2)/L; n = v(3)/L;
D_xy = sqrt(l^2 + m^2);
if abs(n - 1) < 1e-9
    lam = [0, 0, 1; 0, 1, 0; -1, 0, 0];
elseif abs(n + 1) < 1e-9
    lam = [0, 0, -1; 0, 1, 0; 1, 0, 0];
else
    lam = [l, m, n;
           -m/D_xy, l/D_xy, 0;
           (-l*n)/D_xy, (-m*n)/D_xy, D_xy];
end
T = zeros(12, 12);
for k = 0:3
    T(3*k+1:3*k+3, 3*k+1:3*k+3) = lam;
end
end
