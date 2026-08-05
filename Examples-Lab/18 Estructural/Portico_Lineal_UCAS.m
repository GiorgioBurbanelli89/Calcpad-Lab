% ============================================================
%  PORTICO 2D LINEAL  —  rigidez directa (metodo UCAS 2D)
%  Elemento frame de 6 GDL (axial + flexion Euler-Bernoulli),
%  ensamblaje 3 GDL/nodo, K u = F, fuerzas de extremo por
%  elemento (N,V,M) y DIAGRAMA DE MOMENTOS. Modelo como DATOS.
% ============================================================

% Nodos: [ id  x  y ]  (m)
XY = [ 1  0  0
       2  0  3
       3  4  3
       4  4  0 ];
% Elementos: [ id  nI  nJ   E(kPa)   A(m2)   I(m4) ]
EL = [ 1  1  2   200e6   0.010   1.0e-4     % columna izq
       2  2  3   200e6   0.010   1.5e-4     % viga
       3  4  3   200e6   0.010   1.0e-4 ];  % columna der

% Cargas nodales: [ nodo  Fx(kN)  Fy(kN)  M(kN*m) ]
LOADS = [ 2   30   -50   0
          3    0   -50   0 ];

nN = size(XY,1);   nE = size(EL,1);   ndof = 3*nN;

% Empotramientos en las bases (nodos 1 y 4)
fixed = [3*1-2 3*1-1 3*1, 3*4-2 3*4-1 3*4];
free  = setdiff(1:ndof, fixed);

% --- Vector de fuerzas ---
F = zeros(ndof,1);
for k = 1:size(LOADS,1)
    n = LOADS(k,1);
    F(3*n-2) = F(3*n-2) + LOADS(k,2);
    F(3*n-1) = F(3*n-1) + LOADS(k,3);
    F(3*n)   = F(3*n)   + LOADS(k,4);
end

% --- Ensamblaje de K ---
K = zeros(ndof, ndof);
for e = 1:nE
    NI = EL(e,2);  NJ = EL(e,3);
    Ee = EL(e,4);  Ae = EL(e,5);  Ie = EL(e,6);
    dx = XY(NJ,2)-XY(NI,2);  dy = XY(NJ,3)-XY(NI,3);
    L  = sqrt(dx^2+dy^2);    c = dx/L;  sn = dy/L;
    ea = Ee*Ae/L;   ei = Ee*Ie;
    kloc = [ ea, 0,        0,       -ea, 0,        0
             0,  12*ei/L^3, 6*ei/L^2, 0, -12*ei/L^3, 6*ei/L^2
             0,  6*ei/L^2,  4*ei/L,   0, -6*ei/L^2,  2*ei/L
            -ea, 0,        0,        ea, 0,        0
             0, -12*ei/L^3,-6*ei/L^2, 0, 12*ei/L^3, -6*ei/L^2
             0,  6*ei/L^2,  2*ei/L,   0, -6*ei/L^2,  4*ei/L ];
    T = [ c sn 0 0 0 0; -sn c 0 0 0 0; 0 0 1 0 0 0
          0 0 0 c sn 0; 0 0 0 -sn c 0; 0 0 0 0 0 1 ];
    kg = T' * kloc * T;
    g  = [3*NI-2, 3*NI-1, 3*NI, 3*NJ-2, 3*NJ-1, 3*NJ];
    K(g,g) = K(g,g) + kg;
end

% --- Solucion  K u = F ---
u = zeros(ndof,1);
u(free) = inv(K(free,free)) * F(free);
R = K(fixed,:) * u - F(fixed);          % reacciones

% --- Fuerzas de extremo por elemento (locales: N,V,M en I y J) --------
disp('=== Fuerzas de extremo por elemento (local) ===')
Mend = zeros(nE,2);
for e = 1:nE
    NI = EL(e,2);  NJ = EL(e,3);
    Ee = EL(e,4);  Ae = EL(e,5);  Ie = EL(e,6);
    dx = XY(NJ,2)-XY(NI,2);  dy = XY(NJ,3)-XY(NI,3);
    L  = sqrt(dx^2+dy^2);    c = dx/L;  sn = dy/L;
    ea = Ee*Ae/L;   ei = Ee*Ie;
    kloc = [ ea, 0,        0,       -ea, 0,        0
             0,  12*ei/L^3, 6*ei/L^2, 0, -12*ei/L^3, 6*ei/L^2
             0,  6*ei/L^2,  4*ei/L,   0, -6*ei/L^2,  2*ei/L
            -ea, 0,        0,        ea, 0,        0
             0, -12*ei/L^3,-6*ei/L^2, 0, 12*ei/L^3, -6*ei/L^2
             0,  6*ei/L^2,  2*ei/L,   0, -6*ei/L^2,  4*ei/L ];
    T = [ c sn 0 0 0 0; -sn c 0 0 0 0; 0 0 1 0 0 0
          0 0 0 c sn 0; 0 0 0 -sn c 0; 0 0 0 0 0 1 ];
    g  = [3*NI-2, 3*NI-1, 3*NI, 3*NJ-2, 3*NJ-1, 3*NJ];
    fl = kloc * (T * u(g));    % fuerzas locales de extremo
    Mend(e,:) = [fl(3), fl(6)];
    fprintf('Elem %d:  N=%8.2f  Vi=%8.2f  Mi=%8.2f  |  Vj=%8.2f  Mj=%8.2f  (kN,kN*m)\n', ...
            e, fl(1), fl(2), fl(3), fl(5), fl(6));
end
disp('=== Reacciones en las bases (Fx,Fy,M) ===')
disp(R')

% --- Grafico: portico + DIAGRAMA DE MOMENTOS -------------------------
figure; hold on; grid on; axis equal
esc = 0.004;                    % escala del diagrama de momentos (m por kN*m)
for e = 1:nE
    NI = EL(e,2);  NJ = EL(e,3);
    xi = XY(NI,2); yi = XY(NI,3);  xj = XY(NJ,2); yj = XY(NJ,3);
    dx = xj-xi;  dy = yj-yi;  L = sqrt(dx^2+dy^2);
    c = dx/L;  sn = dy/L;   nx = -sn;  ny = c;    % normal al elemento
    % Momentos de extremo (convencion: dibujar del lado de traccion)
    Mi = -Mend(e,1);   Mj =  Mend(e,2);
    % puntos del diagrama (lineal entre extremos, cargas nodales)
    p1x = xi + esc*Mi*nx;   p1y = yi + esc*Mi*ny;
    p2x = xj + esc*Mj*nx;   p2y = yj + esc*Mj*ny;
    % elemento (negro)
    plot([xi xj],[yi yj],'-k','LineWidth',2.5);
    % relleno del momento (azul)
    plot([xi p1x],[yi p1y],'-b');
    plot([p1x p2x],[p1y p2y],'-b','LineWidth',1.5);
    plot([xj p2x],[yj p2y],'-b');
end
plot(XY(:,2),XY(:,3),'ko','MarkerFaceColor','k','MarkerSize',7);
title('Portico 2D lineal (UCAS) — negro: estructura, azul: diagrama de momentos');
xlabel('x (m)');  ylabel('y (m)');
