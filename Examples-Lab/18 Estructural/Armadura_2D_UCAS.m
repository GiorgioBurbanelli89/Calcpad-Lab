% ============================================================
%  ARMADURA 2D  —  metodo replicado de UCAS 2D (armadurac.m)
%  Rigidez directa: por barra  L, angulo, B(4x4), kl = EA/L,
%  kg = B'*kl*B ; ensamblaje por GDL ; Up = inv(Kpp)*Fp ;
%  reacciones, fuerzas axiales y deformada.
%  El modelo se define como DATOS (no se dibuja con el mouse).
% ============================================================

% --- Modelo como DATOS -----------------------------------------------
% XY:  [ id   x    y   restrX restrY   Fx     Fy ]     (m , kN)
XY = [ 1   0    0     1     1      0      0
       2   4    0     0     1      0      0
       3   2    3     0     0      0    -50 ];

% CONN: [ id  nodoI  nodoJ   E(kPa)    alfa   A(m2) ]
CONN = [ 1    1      2      200e6      0     0.001
         2    1      3      200e6      0     0.001
         3    2      3      200e6      0     0.001 ];

E  = CONN(:,4);   A = CONN(:,6);
nN = size(XY,1);  nE = size(CONN,1);

% --- Fuerzas y restricciones (2 GDL por nodo), como UCAS -------------
Fext = zeros(2*nN,1);   Rest = zeros(2*nN,1);
for i = 1:nN
    Fext(2*i-1) = XY(i,6);   Fext(2*i) = XY(i,7);
    Rest(2*i-1) = XY(i,4);   Rest(2*i) = XY(i,5);
end
s = find(Rest);      % GDL restringidos (apoyos)
p = find(~Rest);     % GDL libres

% --- Matriz de rigidez global  K = suma( B'*kl*B ) -------------------
K = zeros(2*nN, 2*nN);
for i = 1:nE
    NI = find(CONN(i,2)==XY(:,1));
    NJ = find(CONN(i,3)==XY(:,1));
    dx = XY(NJ,2)-XY(NI,2);   dy = XY(NJ,3)-XY(NI,3);
    L  = sqrt(dx^2 + dy^2);
    c  = dx/L;   sn = dy/L;
    B  = [ c  sn  0  0
          -sn  c  0  0
           0  0   c  sn
           0  0  -sn  c ];
    kl = (E(i)*A(i)/L) * [ 1 0 -1 0
                           0 0  0 0
                          -1 0  1 0
                           0 0  0 0 ];
    kg = B' * kl * B;
    g  = [2*NI-1, 2*NI, 2*NJ-1, 2*NJ];
    K(g,g) = K(g,g) + kg;
end

% --- Solucion   F = K*u ----------------------------------------------
Kpp = K(p,p);   Ksp = K(s,p);
Fp  = Fext(p);
Up  = inv(Kpp) * Fp;         % (UCAS usa inv(Kpp)*Fp)
R   = Ksp * Up;              % reacciones en apoyos
u   = zeros(2*nN,1);   u(p) = Up;

% --- Resultados -------------------------------------------------------
disp('=== Desplazamientos nodales (m) ===')
for i = 1:nN
    fprintf('Nodo %d:  ux = %11.4e   uy = %11.4e\n', XY(i,1), u(2*i-1), u(2*i));
end
disp('=== Fuerzas axiales en barras (kN,  + traccion) ===')
for i = 1:nE
    NI = find(CONN(i,2)==XY(:,1));
    NJ = find(CONN(i,3)==XY(:,1));
    dx = XY(NJ,2)-XY(NI,2);   dy = XY(NJ,3)-XY(NI,3);
    L  = sqrt(dx^2 + dy^2);   c = dx/L;   sn = dy/L;
    ue = [u(2*NI-1); u(2*NI); u(2*NJ-1); u(2*NJ)];
    Trow = [-c, -sn, c, sn];
    Nax = (E(i)*A(i)/L) * (Trow * ue);
    fprintf('Barra %d (%d-%d):  N = %9.3f kN\n', CONN(i,1), CONN(i,2), CONN(i,3), Nax(1));
end
disp('=== Reacciones en apoyos (kN) ===')
disp(R')

% --- Grafico: original (negro) + deformada x escala (azul) -----------
XYo = XY(:,2:3);   esc = 20;
figure; hold on; grid on; axis equal
for i = 1:nE
    NI = find(CONN(i,2)==XY(:,1));
    NJ = find(CONN(i,3)==XY(:,1));
    xo = [XYo(NI,1) XYo(NJ,1)];   yo = [XYo(NI,2) XYo(NJ,2)];
    xd = [XYo(NI,1)+esc*u(2*NI-1)  XYo(NJ,1)+esc*u(2*NJ-1)];
    yd = [XYo(NI,2)+esc*u(2*NI)    XYo(NJ,2)+esc*u(2*NJ)];
    plot(xo, yo, '-k', 'LineWidth', 2.5);
    plot(xd, yd, '--b', 'LineWidth', 1.5);
end
plot(XYo(:,1), XYo(:,2), 'ko', 'MarkerFaceColor', 'k', 'MarkerSize', 7);
title('Armadura 2D (metodo UCAS) — negro: original,  azul: deformada x20');
xlabel('x (m)');   ylabel('y (m)');
