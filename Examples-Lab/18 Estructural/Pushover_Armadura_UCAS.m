% ============================================================
%  PUSHOVER de ARMADURA  —  analisis NO LINEAL (metodo UCAS 2D)
%  Event-to-event (armadurac.m, seccion no lineal):
%   - se aplica carga UNITARIA en el nodo de control;
%   - factor para fluir cada barra:  Pf = (Py - N_acum)/|n_unit| ;
%   - la barra con MENOR Pf fluye, se ablanda (E -> ~0, plastica);
%   - se re-arma la rigidez y se repite -> CURVA DE CAPACIDAD (V vs U).
%  Modelo: armadura de 3 barras estaticamente INDETERMINADA (clasica).
% ============================================================

% --- Modelo como DATOS -----------------------------------------------
% XY: [ id   x    y   restrX restrY   Fx   Fy ]      (m , kN)
XY = [ 1   0    3     1     1      0    0
       2   2    3     1     1      0    0
       3   4    3     1     1      0    0
       4   2    0     0     0      0    0 ];   % nodo 4 = control (libre)

% CONN: [ id  nodoI  nodoJ   E(kPa)    alfa   A(m2) ]
CONN = [ 1    1      4      200e6      0     0.001
         2    2      4      200e6      0     0.001
         3    3      4      200e6      0     0.001 ];

E0 = CONN(:,4);   A = CONN(:,6);
nN = size(XY,1);  nE = size(CONN,1);

Fy   = 250e3;          % esfuerzo de fluencia (kPa ~ acero 250 MPa)
Py   = Fy * A;         % capacidad axial por barra (kN)
GDLc = 2*4;            % GDL de control: nodo 4, uy (empuje vertical)

% Restricciones (2 GDL por nodo)
Rest = zeros(2*nN,1);
for i = 1:nN
    Rest(2*i-1) = XY(i,4);   Rest(2*i) = XY(i,5);
end
s = find(Rest);      p = find(~Rest);

% Estado no lineal
Emod    = E0;              % modulo actual por barra (se ablanda al fluir)
yielded = zeros(nE,1);     % 1 si la barra ya fluyo
Nacc    = zeros(nE,1);     % fuerza axial acumulada por barra
Pacc = 0;  Uacc = 0;
Ucurve = 0;  Vcurve = 0;   % curva de capacidad (parte del origen)

disp('=== Secuencia de fluencia (pushover event-to-event) ===')
for step = 1:nE
    % --- Rigidez global con los modulos actuales ---
    K = zeros(2*nN, 2*nN);
    for i = 1:nE
        NI = find(CONN(i,2)==XY(:,1));   NJ = find(CONN(i,3)==XY(:,1));
        dx = XY(NJ,2)-XY(NI,2);   dy = XY(NJ,3)-XY(NI,3);
        L  = sqrt(dx^2 + dy^2);   c = dx/L;   sn = dy/L;
        B  = [ c sn 0 0; -sn c 0 0; 0 0 c sn; 0 0 -sn c ];
        kl = (Emod(i)*A(i)/L) * [1 0 -1 0; 0 0 0 0; -1 0 1 0; 0 0 0 0];
        kg = B' * kl * B;
        g  = [2*NI-1, 2*NI, 2*NJ-1, 2*NJ];
        K(g,g) = K(g,g) + kg;
    end

    % --- Resolver carga UNITARIA en el nodo de control (hacia abajo) ---
    Fu = zeros(2*nN,1);   Fu(GDLc) = -1;
    Kpp = K(p,p);
    uu  = zeros(2*nN,1);
    uu(p) = inv(Kpp) * Fu(p);

    % --- Fuerzas axiales por barra bajo la carga unitaria ---
    lambda = inf;   ey = 0;
    nunit = zeros(nE,1);
    for i = 1:nE
        NI = find(CONN(i,2)==XY(:,1));   NJ = find(CONN(i,3)==XY(:,1));
        dx = XY(NJ,2)-XY(NI,2);   dy = XY(NJ,3)-XY(NI,3);
        L  = sqrt(dx^2 + dy^2);   c = dx/L;   sn = dy/L;
        ue = [uu(2*NI-1); uu(2*NI); uu(2*NJ-1); uu(2*NJ)];
        Trow = [-c, -sn, c, sn];
        nvec = (Emod(i)*A(i)/L) * (Trow * ue);
        nunit(i) = nvec(1);
        % factor de carga para llevar esta barra (aun elastica) a fluir
        if yielded(i) == 0 && abs(nunit(i)) > 1e-9
            pf = (Py(i) - Nacc(i)) / abs(nunit(i));
            if pf < lambda
                lambda = pf;   ey = i;
            end
        end
    end
    if ey == 0
        disp('Mecanismo alcanzado: no quedan barras elasticas.')
        break
    end

    % --- Acumular el incremento hasta el proximo evento de fluencia ---
    Nacc = Nacc + lambda * abs(nunit);
    Pacc = Pacc + lambda;
    Uacc = Uacc + lambda * abs(uu(GDLc));
    Ucurve = [Ucurve; Uacc];
    Vcurve = [Vcurve; Pacc];
    fprintf('Paso %d: fluye barra %d  |  V_base = %8.2f kN   U = %.4e m\n', ...
            step, ey, Pacc, Uacc);

    % --- Ablandar la barra que fluyo (plastica: E -> ~0) ---
    yielded(ey) = 1;
    Emod(ey)    = E0(ey) * 1e-3;
end

% --- Curva de capacidad (pushover) -----------------------------------
figure; hold on; grid on
plot(Ucurve, Vcurve, '-o', 'LineWidth', 2, 'Color', [0.10 0.30 0.75]);
xlabel('Desplazamiento del nodo de control   U (m)');
ylabel('Cortante base   V (kN)');
title('Curva de capacidad (pushover) — armadura 3 barras, metodo UCAS');
