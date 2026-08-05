% ============================================================
%  ARMADURA 2D con CARGA DE TEMPERATURA  (metodo UCAS 2D)
%  UCAS lleva el coef. de dilatacion 'alfa' en la conectividad y
%  arma fuerzas equivalentes por temperatura. Una barra calentada
%  DT desarrolla una fuerza termica  fT = E*A*alfa*DT ; en una
%  armadura ESTATICAMENTE INDETERMINADA eso genera AUTO-TENSIONES
%  (fuerzas internas sin carga externa). Aqui se calientan todas
%  las barras y se obtienen desplazamientos + fuerzas axiales.
% ============================================================

% --- Modelo como DATOS (armadura de 3 barras indeterminada) ----------
% XY: [ id   x    y   restrX restrY ]     (m)
XY = [ 1   0    3     1     1
       2   2    3     1     1
       3   4    3     1     1
       4   2    0     0     0 ];   % nodo 4 = libre

% CONN: [ id  nodoI  nodoJ   E(kPa)   alfa(1/C)   A(m2) ]
CONN = [ 1    1      4      200e6    1.2e-5      0.001
         2    2      4      200e6    1.2e-5      0.001
         3    3      4      200e6    1.2e-5      0.001 ];

DT = 50;                 % incremento de temperatura (C) en TODAS las barras

E = CONN(:,4);  alfa = CONN(:,5);  A = CONN(:,6);
nN = size(XY,1);  nE = size(CONN,1);

% Restricciones (2 GDL por nodo)
Rest = zeros(2*nN,1);
for i = 1:nN
    Rest(2*i-1) = XY(i,4);   Rest(2*i) = XY(i,5);
end
s = find(Rest);   p = find(~Rest);

% --- Rigidez global + fuerzas equivalentes por temperatura -----------
K  = zeros(2*nN, 2*nN);
FT = zeros(2*nN, 1);        % vector de fuerzas termicas equivalentes
fT = zeros(nE,1);           % fuerza termica por barra (para recuperar N despues)
for i = 1:nE
    NI = find(CONN(i,2)==XY(:,1));   NJ = find(CONN(i,3)==XY(:,1));
    dx = XY(NJ,2)-XY(NI,2);   dy = XY(NJ,3)-XY(NI,3);
    L  = sqrt(dx^2+dy^2);     c = dx/L;   sn = dy/L;
    B  = [ c sn 0 0; -sn c 0 0; 0 0 c sn; 0 0 -sn c ];
    kl = (E(i)*A(i)/L) * [1 0 -1 0; 0 0 0 0; -1 0 1 0; 0 0 0 0];
    kg = B' * kl * B;
    g  = [2*NI-1, 2*NI, 2*NJ-1, 2*NJ];
    K(g,g) = K(g,g) + kg;
    % Fuerza termica de la barra: la barra calentada empuja sus extremos
    % hacia afuera con  fT = E*A*alfa*DT. En local: [-fT, 0, +fT, 0].
    fT(i) = E(i)*A(i)*alfa(i)*DT;
    floc  = [-fT(i); 0; fT(i); 0];
    fglob = B' * floc;
    FT(g) = FT(g) + fglob;
end

% --- Solucion  K u = FT ----------------------------------------------
Kpp = K(p,p);   Ksp = K(s,p);
u   = zeros(2*nN,1);
u(p) = inv(Kpp) * FT(p);
R    = Ksp * u(p) - FT(s);     % reacciones (deben sumar ~0: sin carga externa)

% --- Resultados -------------------------------------------------------
fprintf('Temperatura aplicada: DT = %g C  (fuerza termica por barra fT = %.1f kN)\n', DT, fT(1));
disp('=== Desplazamientos nodales (m) ===')
for i = 1:nN
    fprintf('Nodo %d:  ux = %11.4e   uy = %11.4e\n', XY(i,1), u(2*i-1), u(2*i));
end
disp('=== Fuerzas axiales por barra (kN,  + traccion) ===')
% N = E*A/L*(alargamiento mecanico) - fT   (se resta la parte termica libre)
for i = 1:nE
    NI = find(CONN(i,2)==XY(:,1));   NJ = find(CONN(i,3)==XY(:,1));
    dx = XY(NJ,2)-XY(NI,2);   dy = XY(NJ,3)-XY(NI,3);
    L  = sqrt(dx^2+dy^2);     c = dx/L;   sn = dy/L;
    ue = [u(2*NI-1); u(2*NI); u(2*NJ-1); u(2*NJ)];
    Trow = [-c, -sn, c, sn];
    Nmech = (E(i)*A(i)/L) * (Trow * ue);
    Nax   = Nmech(1) - fT(i);
    fprintf('Barra %d (%d-%d):  N = %9.3f kN\n', CONN(i,1), CONN(i,2), CONN(i,3), Nax);
end
disp('=== Reacciones en apoyos (kN) — deben sumar ~0 (auto-tension) ===')
disp(R')

% --- Grafico: armadura original + deformada por temperatura ----------
XYo = XY(:,2:3);   esc = 200;     % la deformada termica es pequena -> escala grande
figure; hold on; grid on; axis equal
for i = 1:nE
    NI = find(CONN(i,2)==XY(:,1));   NJ = find(CONN(i,3)==XY(:,1));
    xo = [XYo(NI,1) XYo(NJ,1)];   yo = [XYo(NI,2) XYo(NJ,2)];
    xd = [XYo(NI,1)+esc*u(2*NI-1)  XYo(NJ,1)+esc*u(2*NJ-1)];
    yd = [XYo(NI,2)+esc*u(2*NI)    XYo(NJ,2)+esc*u(2*NJ)];
    plot(xo, yo, '-k', 'LineWidth', 2.5);
    plot(xd, yd, '--r', 'LineWidth', 1.5);
end
plot(XYo(:,1), XYo(:,2), 'ko', 'MarkerFaceColor', 'k', 'MarkerSize', 7);
title(sprintf('Armadura con DT=%g C (UCAS) — negro: original, rojo: deformada termica x%g', DT, esc));
xlabel('x (m)');   ylabel('y (m)');
