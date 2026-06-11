function [Mxx, Myy, Mxy, Fxx, Fyy, V13, V23] = shell_centroid(elm, u, x, y, E, nu, t)
% Shell resultants en el centroide (Q4 axis-aligned).
% Port hekatan_fem/utils/_shell_resultants_at_centroid.
a = (max(x) - min(x)) / 2;
b = (max(y) - min(y)) / 2;
D = E * t^3 / (12 * (1 - nu^2));
Em_t = E * t / (1 - nu^2);
ks_sh = 5/6;
G = E / (2*(1 + nu));
dNdxi  = [-0.25, 0.25, 0.25, -0.25];
dNdeta = [-0.25, -0.25, 0.25, 0.25];
N_c    = [0.25, 0.25, 0.25, 0.25];
uL = zeros(4, 6);
for k = 1:4
    g = elm(k);
    uL(k, :) = u(6*g-5 : 6*g)';
end
kxx = 0; kyy = 0; kxy = 0;
exx = 0; eyy = 0;
dwdx = 0; dwdy = 0; tx_c = 0; ty_c = 0;
for k = 1:4
    ux = uL(k, 1); uy = uL(k, 2); w = uL(k, 3);
    tx = uL(k, 4); ty = uL(k, 5);
    dx_ = dNdxi(k)  / a;
    dy_ = dNdeta(k) / b;
    kxx = kxx + (-dx_ * ty);
    kyy = kyy + ( dy_ * tx);
    kxy = kxy + ( dx_ * tx - dy_ * ty);
    exx = exx + (dx_ * ux);
    eyy = eyy + (dy_ * uy);
    dwdx = dwdx + (dx_ * w);
    dwdy = dwdy + (dy_ * w);
    tx_c = tx_c + N_c(k) * tx;
    ty_c = ty_c + N_c(k) * ty;
end
Mxx = D * (kxx + nu*kyy);
Myy = D * (nu*kxx + kyy);
Mxy = D * (1-nu)/2 * kxy;
Fxx = Em_t * (exx + nu*eyy);
Fyy = Em_t * (nu*exx + eyy);
V13 = ks_sh * G * t * (dwdx + ty_c);
V23 = ks_sh * G * t * (dwdy - tx_c);
end
