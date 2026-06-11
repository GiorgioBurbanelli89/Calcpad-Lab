function K = kbend_mzc(x, y, E, nu, t)
% MZC Kirchhoff plate bending Q4 12x12.
% Port hekatan_fem/utils/shell_thin.py::_bending_k_mzc
% Asume Q4 rectangular axis-aligned. DOFs por nodo: [w, tx, ty].
a = (max(x) - min(x)) / 2;
b = (max(y) - min(y)) / 2;
D0 = E * t^3 / (12 * (1 - nu^2));
D = [D0, D0*nu, 0;
     D0*nu, D0,  0;
     0,    0,   D0*(1-nu)/2];
xi_n  = [-1, 1, 1, -1];
eta_n = [-1, -1, 1, 1];
gp = [-sqrt(3/5), 0, sqrt(3/5)];
gw = [5/9, 8/9, 5/9];
K = zeros(12, 12);
for ig = 1:3
    for jg = 1:3
        xi = gp(ig); eta = gp(jg);
        w_int = gw(ig) * gw(jg);
        B = zeros(3, 12);
        for i = 1:4
            xi_i = xi_n(i); eta_i = eta_n(i);
            xi_ii  = xi_i * xi;
            eta_ii = eta_i * eta;
            term1 = (1 + xi_ii) * (1 + eta_ii);
            t2_   = 2 + xi_ii + eta_ii - xi^2 - eta^2;
            dt1_dxi  = xi_i * (1 + eta_ii);
            dt2_dxi  = xi_i - 2*xi;
            dt1_deta = eta_i * (1 + xi_ii);
            dt2_deta = eta_i - 2*eta;
            d2H1_dxi2    = (1/8) * (term1*(-2) + 2*dt1_dxi*dt2_dxi);
            d2H1_deta2   = (1/8) * (term1*(-2) + 2*dt1_deta*dt2_deta);
            d2t1_dxideta = xi_i * eta_i;
            d2H1_dxideta = (1/8) * (d2t1_dxideta*t2_ + dt1_dxi*dt2_deta + dt1_deta*dt2_dxi);
            d2H2_dxi2    = 0;
            d2H2_deta2   = (b/8) * eta_i * (1 + xi_ii) * 2 * (3*eta_ii + 1) * eta_i * eta_i;
            d2H2_dxideta = (b/8) * xi_i * eta_i * eta_i * (1 + eta_ii) * (3*eta_ii - 1);
            d2H3_dxi2    = -(a/8) * xi_i * (1 + eta_ii) * 2 * (3*xi_ii + 1) * xi_i * xi_i;
            d2H3_deta2   = 0;
            d2H3_dxideta = -(a/8) * eta_i * xi_i * xi_i * (1 + xi_ii) * (3*xi_ii - 1);
            col_w  = 3*(i-1) + 1;
            col_tx = 3*(i-1) + 2;
            col_ty = 3*(i-1) + 3;
            B(1, col_w)  = -d2H1_dxi2  / (a^2);
            B(2, col_w)  = -d2H1_deta2 / (b^2);
            B(3, col_w)  = -2 * d2H1_dxideta / (a*b);
            B(1, col_tx) = -d2H2_dxi2  / (a^2);
            B(2, col_tx) = -d2H2_deta2 / (b^2);
            B(3, col_tx) = -2 * d2H2_dxideta / (a*b);
            B(1, col_ty) = -d2H3_dxi2  / (a^2);
            B(2, col_ty) = -d2H3_deta2 / (b^2);
            B(3, col_ty) = -2 * d2H3_dxideta / (a*b);
        end
        detJ = a * b;
        K = K + w_int * (B' * D * B) * detJ;
    end
end
end
