% ============================================================
%  PUSHOVER de PORTICO 2D  —  rotulas plasticas, event-to-event
%  (metodo del pushover no lineal de UCAS 2D, NSP_Pushover_armadura.m,
%   simplificado a rotulas concentradas / plasticidad por momento):
%   - carga lateral de REFERENCIA en el techo;
%   - se calculan los momentos de extremo; el extremo que primero
%     alcanza Mp forma una ROTULA (release de momento);
%   - se re-arma la rigidez y se repite -> curva de capacidad V vs U,
%     hasta el MECANISMO (rigidez lateral ~ 0).
%  Modelo: portico portal (2 columnas empotradas + 1 viga).
% ============================================================

% --- Modelo como DATOS -----------------------------------------------
% Nodos: [ id  x  y ]   (m)
XY = [ 1  0  0
       2  0  3
       3  4  3
       4  4  0 ];
% Elementos: [ id  nI  nJ   E(kPa)   A(m2)    I(m4)     Mp(kN*m) ]
EL = [ 1  1  2   200e6   0.010   1.0e-4    150     % columna izq
       2  2  3   200e6   0.010   1.5e-4    180     % viga
       3  4  3   200e6   0.010   1.0e-4    150 ];  % columna der

nN = size(XY,1);   nE = size(EL,1);
ndof = 3*nN;                      % 3 GDL por nodo: ux, uy, giro

% Empotramientos en las bases (nodos 1 y 4): todos los GDL fijos
fixed = [1 2 3, 10 11 12];        % (3*1-2..3*1) y (3*4-2..3*4)
free  = setdiff(1:ndof, fixed);

% GDL de control: ux del nodo 2 (deriva de techo). Carga lateral de referencia ahi.
GDLc = 3*2-2;                     % = 4

% Estado de rotulas: hi(e), hj(e) = 1 si el extremo I / J ya rotulo
hi = zeros(nE,1);   hj = zeros(nE,1);
Macc_i = zeros(nE,1);  Macc_j = zeros(nE,1);   % momento acumulado por extremo
Vacc = 0;  Uacc = 0;
Ucurve = 0;  Vcurve = 0;

disp('=== Secuencia de rotulas plasticas (pushover) ===')
for step = 1:(2*nE+1)
    % --- Ensamblaje global con el estado de rotulas actual ---
    K = zeros(ndof, ndof);
    for e = 1:nE
        NI = EL(e,2);  NJ = EL(e,3);
        Ee = EL(e,4);  Ae = EL(e,5);  Ie = EL(e,6);
        dx = XY(NJ,2)-XY(NI,2);  dy = XY(NJ,3)-XY(NI,3);
        L  = sqrt(dx^2+dy^2);    c = dx/L;  sn = dy/L;
        ea = Ee*Ae/L;   ei = Ee*Ie;
        % --- Parte flexional local segun rotulas (v_i, th_i, v_j, th_j) ---
        if hi(e)==0 && hj(e)==0
            kf = (ei/L^3)*[ 12,   6*L,  -12,   6*L
                            6*L, 4*L^2, -6*L, 2*L^2
                           -12,  -6*L,   12,  -6*L
                            6*L, 2*L^2, -6*L, 4*L^2];
        elseif hi(e)==1 && hj(e)==0        % rotula en extremo I
            kf = (ei/L^3)*[ 3, 0, -3,  3*L
                            0, 0,  0,  0
                           -3, 0,  3, -3*L
                            3*L,0,-3*L,3*L^2];
        elseif hi(e)==0 && hj(e)==1        % rotula en extremo J
            kf = (ei/L^3)*[ 3,  3*L, -3, 0
                            3*L,3*L^2,-3*L,0
                           -3, -3*L,  3, 0
                            0,  0,    0, 0];
        else                                % rotula en ambos -> solo axial
            kf = zeros(4,4);
        end
        % --- Ensamblar 6x6 local: gdl [u_i v_i th_i u_j v_j th_j] ---
        kloc = zeros(6,6);
        kloc([1 4],[1 4]) = [ ea, -ea; -ea, ea ];
        kloc([2 3 5 6],[2 3 5 6]) = kf;
        % --- Transformacion a global ---
        T = [ c  sn 0 0  0 0
             -sn c  0 0  0 0
              0  0  1 0  0 0
              0  0  0 c  sn 0
              0  0  0 -sn c 0
              0  0  0 0  0 1];
        kg = T' * kloc * T;
        g  = [3*NI-2, 3*NI-1, 3*NI, 3*NJ-2, 3*NJ-1, 3*NJ];
        K(g,g) = K(g,g) + kg;
    end

    % --- Carga lateral de REFERENCIA (unitaria) en el techo ---
    F = zeros(ndof,1);   F(GDLc) = 1;
    Kff = K(free,free);
    % Detectar mecanismo: rigidez lateral casi nula
    u = zeros(ndof,1);
    u(free) = inv(Kff) * F(free);
    if abs(u(GDLc)) > 1e6 || ~isfinite(u(GDLc))
        disp('Mecanismo alcanzado (rigidez lateral ~ 0).')
        break
    end

    % --- Momentos de extremo por elemento (bajo la carga unitaria) ---
    lambda = inf;   ee = 0;   endsel = 0;
    Mi = zeros(nE,1);  Mj = zeros(nE,1);
    for e = 1:nE
        NI = EL(e,2);  NJ = EL(e,3);
        Ee = EL(e,4);  Ie = EL(e,6);
        dx = XY(NJ,2)-XY(NI,2);  dy = XY(NJ,3)-XY(NI,3);
        L  = sqrt(dx^2+dy^2);    c = dx/L;  sn = dy/L;
        T = [ c  sn 0 0  0 0
             -sn c  0 0  0 0
              0  0  1 0  0 0
              0  0  0 c  sn 0
              0  0  0 -sn c 0
              0  0  0 0  0 1];
        g   = [3*NI-2, 3*NI-1, 3*NI, 3*NJ-2, 3*NJ-1, 3*NJ];
        ul  = T * u(g);            % desplazamientos locales [u_i v_i th_i u_j v_j th_j]
        ei  = Ee*Ie;
        % Momento de extremo (Euler-Bernoulli), solo si el extremo NO esta rotulado
        if hi(e)==0
            Mi(e) = (ei/L^2)*( 6*ul(2) + 4*L*ul(3) - 6*ul(5) + 2*L*ul(6) )/L;
            rem = EL(e,7) - Macc_i(e);
            if abs(Mi(e))>1e-9
                pf = rem/abs(Mi(e));
                if pf < lambda, lambda=pf; ee=e; endsel=1; end
            end
        end
        if hj(e)==0
            Mj(e) = (ei/L^2)*( 6*ul(2) + 2*L*ul(3) - 6*ul(5) + 4*L*ul(6) )/L;
            rem = EL(e,7) - Macc_j(e);
            if abs(Mj(e))>1e-9
                pf = rem/abs(Mj(e));
                if pf < lambda, lambda=pf; ee=e; endsel=2; end
            end
        end
    end
    if ee==0
        disp('No quedan extremos elasticos (mecanismo).')
        break
    end

    % --- Acumular hasta el proximo evento de rotula ---
    Macc_i = Macc_i + lambda*abs(Mi);
    Macc_j = Macc_j + lambda*abs(Mj);
    Vacc = Vacc + lambda;
    Uacc = Uacc + lambda*abs(u(GDLc));
    Ucurve = [Ucurve; Uacc];
    Vcurve = [Vcurve; Vacc];
    if endsel==1
        hi(ee)=1;  extremo='I';
    else
        hj(ee)=1;  extremo='J';
    end
    fprintf('Paso %d: rotula en elem %d extremo %s  |  V_base = %8.2f kN   U = %.4e m\n', ...
            step, ee, extremo, Vacc, Uacc);
end

% --- Curva de capacidad (pushover) -----------------------------------
figure; hold on; grid on
plot(Ucurve, Vcurve, '-o', 'LineWidth', 2, 'Color', [0.75 0.20 0.10]);
xlabel('Deriva de techo   U (m)');
ylabel('Cortante base   V (kN)');
title('Curva de capacidad (pushover) — portico portal, rotulas plasticas (UCAS)');
