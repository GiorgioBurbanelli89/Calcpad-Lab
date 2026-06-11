function K = kmembrane_q4(x, y, E, nu, t)
% Plane stress Q4 8x8 (2x2 Gauss).
% Port hekatan_fem/utils/shell_thin.py::_membrane_k_thin
c = E / (1 - nu^2);
Em = [c, c*nu, 0;
      c*nu, c, 0;
      0, 0, c*(1-nu)/2];
gp = [-1/sqrt(3), 1/sqrt(3)];
gw = [1, 1];
K = zeros(8, 8);
for ig = 1:2
    for jg = 1:2
        xi = gp(ig); eta = gp(jg);
        dNdxi  = [-0.25*(1-eta),  0.25*(1-eta),  0.25*(1+eta), -0.25*(1+eta)];
        dNdeta = [-0.25*(1-xi),  -0.25*(1+xi),   0.25*(1+xi),   0.25*(1-xi)];
        J11 = dNdxi  * x; J12 = dNdxi  * y;
        J21 = dNdeta * x; J22 = dNdeta * y;
        detJ = J11*J22 - J12*J21;
        Jinv11 =  J22/detJ; Jinv12 = -J12/detJ;
        Jinv21 = -J21/detJ; Jinv22 =  J11/detJ;
        B = zeros(3, 8);
        for i = 1:4
            dNdx = Jinv11*dNdxi(i) + Jinv12*dNdeta(i);
            dNdy = Jinv21*dNdxi(i) + Jinv22*dNdeta(i);
            B(1, 2*(i-1)+1) = dNdx;
            B(2, 2*(i-1)+2) = dNdy;
            B(3, 2*(i-1)+1) = dNdy;
            B(3, 2*(i-1)+2) = dNdx;
        end
        K = K + gw(ig)*gw(jg) * (B' * Em * B) * t * detJ;
    end
end
end
