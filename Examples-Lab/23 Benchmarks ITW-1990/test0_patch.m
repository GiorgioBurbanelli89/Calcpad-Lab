% ============================================================================
%  TEST 0 - PATCH TEST (deformacion constante) - elemento ITW-1990
%  Malla 2x2 con el nudo interior DISTORSIONADO. Se prescribe en el borde un
%  campo lineal u = a*x, v = d*y (deformacion constante ex=a, ey=d, rz=0).
%  El elemento PASA si el nudo interior reproduce el campo lineal EXACTO.
% ============================================================================
tic;
E = 1000;  nu = 0.3;  n = 3;
a = 1e-3;  d = 5e-4;                    % deformaciones prescritas (ex, ey)
% malla 2x2 (nudos 3x3); nudo central (5) distorsionado
xn = [0 1 2  0 1 2  0 1 2];
yn = [0 0 0  1 1 1  2 2 2];
xn(5) = 1.3;  yn(5) = 0.8;             % <- distorsion del nudo interior
x_j = xn(:);  y_j = yn(:);  n_j = 9;
e_j = [1 2 5 4; 2 3 6 5; 4 5 8 7; 5 6 9 8];
D   = E/(1-nu^2) * [1 nu 0; nu 1 0; 0 0 (1-nu)/2];
gam = E/(2*(1+nu));
K = assemble_itw(x_j, y_j, e_j, D, gam, 1);
F = zeros(n*n_j,1);  k_s = 1e12;
bnd = [1 2 3 4 6 7 8 9];               % todos menos el interior (5)
for b = bnd
    ux = a*x_j(b);  uy = d*y_j(b);  rz = 0;    % campo lineal, rz=0
    dofs = [n*(b-1)+1 n*(b-1)+2 n*(b-1)+3];
    vals = [ux uy rz];
    for q = 1:3
        K(dofs(q),dofs(q)) = K(dofs(q),dofs(q)) + k_s;
        F(dofs(q)) = F(dofs(q)) + k_s*vals(q);
    end
end
Z = K\F;
ux5 = Z(n*(5-1)+1);  uy5 = Z(n*(5-1)+2);
ex_u = a*x_j(5);     ex_v = d*y_j(5);
err = max(abs([ux5-ex_u, uy5-ex_v]));
t_seg = toc;
fprintf('==== TEST 0 PATCH TEST (ITW) ====\n');
fprintf('nudo interior:  u = %.6e (exacto %.6e)\n', ux5, ex_u);
fprintf('                v = %.6e (exacto %.6e)\n', uy5, ex_v);
fprintf('error maximo    = %.3e\n', err);
if err < 1e-8
    fprintf('PATCH TEST: PASA (reproduce deformacion constante exacto)\n');
else
    fprintf('PATCH TEST: FALLA (error %.3e)\n', err);
end
fprintf('CHECK t_seg %.4f\n', t_seg);
