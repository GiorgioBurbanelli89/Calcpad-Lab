function K = kframe_local(n0, n1, p)
% Frame Bernoulli-Euler 6DOF local 12x12.
% DOFs por nodo: [ux, uy, uz, tx, ty, tz].
% p struct: {E, nu, A, Iy, Iz, J}.
L = norm(n1 - n0);
E = p.E; A = p.A; Iy = p.Iy; Iz = p.Iz; J = p.J;
G = E / (2*(1+p.nu));
K = zeros(12, 12);
EA_L  = E*A/L;
GJ_L  = G*J/L;
EIy_L = E*Iy/L; EIy_L2 = E*Iy/L^2; EIy_L3 = E*Iy/L^3;
EIz_L = E*Iz/L; EIz_L2 = E*Iz/L^2; EIz_L3 = E*Iz/L^3;
% Axial
K(1,1)=EA_L; K(1,7)=-EA_L; K(7,1)=-EA_L; K(7,7)=EA_L;
% Torsion
K(4,4)=GJ_L; K(4,10)=-GJ_L; K(10,4)=-GJ_L; K(10,10)=GJ_L;
% Bending alrededor de y_local (DOFs 3=uz, 5=ty, 9=uz_j, 11=ty_j)
K(3,3)=12*EIy_L3;  K(3,5)=-6*EIy_L2;  K(3,9)=-12*EIy_L3; K(3,11)=-6*EIy_L2;
K(5,3)=-6*EIy_L2;  K(5,5)=4*EIy_L;    K(5,9)=6*EIy_L2;   K(5,11)=2*EIy_L;
K(9,3)=-12*EIy_L3; K(9,5)=6*EIy_L2;   K(9,9)=12*EIy_L3;  K(9,11)=6*EIy_L2;
K(11,3)=-6*EIy_L2; K(11,5)=2*EIy_L;   K(11,9)=6*EIy_L2;  K(11,11)=4*EIy_L;
% Bending alrededor de z_local (DOFs 2=uy, 6=tz, 8=uy_j, 12=tz_j)
K(2,2)=12*EIz_L3;  K(2,6)=6*EIz_L2;   K(2,8)=-12*EIz_L3; K(2,12)=6*EIz_L2;
K(6,2)=6*EIz_L2;   K(6,6)=4*EIz_L;    K(6,8)=-6*EIz_L2;  K(6,12)=2*EIz_L;
K(8,2)=-12*EIz_L3; K(8,6)=-6*EIz_L2;  K(8,8)=12*EIz_L3;  K(8,12)=-6*EIz_L2;
K(12,2)=6*EIz_L2;  K(12,6)=2*EIz_L;   K(12,8)=-6*EIz_L2; K(12,12)=4*EIz_L;
end
