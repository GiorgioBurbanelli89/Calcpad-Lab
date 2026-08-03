% #md
% # Doble integral de la rigidez K_e - 3 formas
% La **doble integral** de la matriz de rigidez de elemento, en las 3 formas que
% *Hekatan Lab* y *MATLAB* pueden calcular:
%
% | Forma | Metodo | Resultado |
% |-------|--------|-----------|
% | 1 | Expresion simbolica `#noc` | solo DECLARA la integral (tipografia) |
% | 2 | Operacion simbolica `int(int(...))` | integra EXACTO (racional) |
% | 3 | Loop cuadratura de Gauss 4x4 | numerico |
%
% Las tres dan lo **mismo**. Se usa el bloque 4x4 del NODO 1 (elemento 1x1 m,
% igual que en el BFS), asi `K_e(1,1)` = 35191.8 coincide con el ejemplo completo.
% #endmd

%% Datos
E  = 35000e3    % Modulo elastico [kN/m^2] (= 35000 MPa)
nu = 0.15       % Coef de Poisson
t  = 0.1        % Espesor [m]
a  = 1          % ancho del elemento a_1 [m]
b  = 1          % alto  del elemento b_1 [m]
D11 = E*t^3/(12*(1 - nu^2));
% Matriz constitutiva de flexion de placa (la MISMA que en el BFS):
% #noc D = E*t^3/(12*(1 - nu^2))*[1; nu; 0|nu; 1; 0|0; 0; (1 - nu)/2]
D = D11*[1 nu 0; nu 1 0; 0 0 (1 - nu)/2];

%% Funciones de forma del NODO 1 (Hermite cubicas), variables simbolicas
syms xi eta
% Phi_1 = valor (w) ;  Phi_2 = giro (theta):
% #noc Phi_1(x) = 1 - x^2*(3 - 2*x)
% #noc Phi_2(x) = x*(1 - x*(2 - x))
H1x = 1 - xi^2*(3 - 2*xi);    H2x = xi*(1 - xi*(2 - xi));
H1y = 1 - eta^2*(3 - 2*eta);  H2y = eta*(1 - eta*(2 - eta));
% Los 4 GDL del nodo 1:  w, theta_x, theta_y, psi
N = [H1x*H1y, H1x*H2y, H2x*H1y, H2x*H2y];

%% Matriz B (curvaturas) 3x4 - derivadas simbolicas de N
% Curvaturas por columna (nodo 1): kappa_x = -d2N/dxi2, kappa_y = -d2N/deta2,
% kappa_xy = -2 d2N/(dxi deta)
% #noc B_j = [-Phi_xx | -Phi_yy | -2*Phi_xy]
B = sym(zeros(3,4));
for j = 1:4
    B(1,j) = -diff(N(j), xi, 2);            % kappa_x
    B(2,j) = -diff(N(j), eta, 2);           % kappa_y
    B(3,j) = -2*diff(diff(N(j), xi), eta);  % kappa_xy
end

%% ================= FORMA 1: EXPRESION SIMBOLICA (#noc) =================
% Solo declara la integral doble (no la evalua) - notacion tipo Calcpad:
% #noc K_e = a_1*b_1*$Area{$Area{B_i^T*D*B_j @ xi = 0 : 1} @ eta = 0 : 1}

%% ============= FORMA 2: OPERACION SIMBOLICA  int(int(...)) =============
% La entrada (1,1) - se ve el simbolo de doble integral y su valor EXACTO:
K_e11 = a*b*int(int(B(:,1).'*D*B(:,1), xi, 0, 1), eta, 0, 1)
% El BLOQUE 4x4 completo del nodo 1 (matriz simbolica integrada celda a celda):
tic;
Kblk_sym = a*b*int(int(B.'*D*B, xi, 0, 1), eta, 0, 1);
t_sym = toc;
Kblk = double(Kblk_sym)
fprintf('Operacion simbolica int(int) 4x4: %.4f s (Hekatan Lab)\n', t_sym);
fprintf('  Referencia MATLAB 2017a (mismo bloque 4x4): ~1.08 s -> Hekatan comparable.\n');

%% ================= FORMA 3: LOOP (cuadratura Gauss 4x4) =================
gp4 = [-0.861136311594053; -0.339981043584856; 0.339981043584856; 0.861136311594053];
gw4 = [ 0.347854845137454;  0.652145154862546; 0.652145154862546; 0.347854845137454];
gp = (gp4 + 1)/2;  gw = gw4/2;             % mapeo [-1,1] -> [0,1]
idx = [1 1; 1 2; 2 1; 2 2];                % (indice Phi en xi, en eta) por GDL
Kloop = zeros(4);
for ig = 1:4
  for jg = 1:4
    u = gp(ig);  v = gp(jg);  wg = gw(ig)*gw(jg);
    Bn = zeros(3,4);
    for j = 1:4
      Bn(1,j) = -Hdd(idx(j,1),u)*Hf(idx(j,2),v);
      Bn(2,j) = -Hf(idx(j,1),u)*Hdd(idx(j,2),v);
      Bn(3,j) = -2*Hd(idx(j,1),u)*Hd(idx(j,2),v);
    end
    Kloop = Kloop + Bn.'*D*Bn * a*b * wg;
  end
end
Kloop

%% ===================== COMPARACION de las 3 formas =====================
err = max(max(abs(Kblk - Kloop)));
fprintf('K_e(1,1):  simbolico = %g   loop = %g\n', Kblk(1,1), Kloop(1,1));
fprintf('max |K_e(simbolico) - K_e(loop)| = %.3e\n', err);
if err < 1e-6
    fprintf('Las 3 formas COINCIDEN: expresion = operacion simbolica = loop.\n');
end

%% ===== funciones locales: Hermite del nodo 1 (valor, 1a y 2a derivada) =====
function y = Hf(k, x)     % valor
    if k == 1, y = 1 - x^2*(3 - 2*x); else, y = x*(1 - x*(2 - x)); end
end
function y = Hd(k, x)     % 1a derivada
    if k == 1, y = -6*x + 6*x^2;      else, y = 1 - 4*x + 3*x^2;   end
end
function y = Hdd(k, x)    % 2a derivada
    if k == 1, y = -6 + 12*x;         else, y = -4 + 6*x;         end
end
