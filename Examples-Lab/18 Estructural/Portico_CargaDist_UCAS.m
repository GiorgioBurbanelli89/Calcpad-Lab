% ============================================================
%  PORTICO 2D con CARGA DISTRIBUIDA en la viga (metodo UCAS 2D)
%  Frame 6 GDL + FUERZAS DE EMPOTRAMIENTO PERFECTO para carga
%  uniforme w:  cortante wL/2 y momento wL^2/12 en cada extremo.
%  Se pasan a cargas nodales equivalentes, se resuelve, y las
%  fuerzas de extremo reales = k*u_local + fuerzas de empotramiento.
%  El momento de vano parabolico se recupera por superposicion.
% ============================================================

% Nodos: [ id  x  y ]  (m)
XY = [ 1  0  0
       2  0  3
       3  6  3
       4  6  0 ];
% Elementos: [ id  nI  nJ   E(kPa)   A(m2)   I(m4)   w(kN/m) ]
%   w = carga uniforme perpendicular hacia abajo (solo en la viga horizontal)
EL = [ 1  1  2   200e6   0.010   1.5e-4    0        % columna izq
       2  2  3   200e6   0.012   3.0e-4   20        % viga (w=20 kN/m gravedad)
       3  4  3   200e6   0.010   1.5e-4    0 ];     % columna der

nN = size(XY,1);   nE = size(EL,1);   ndof = 3*nN;
fixed = [3*1-2 3*1-1 3*1, 3*4-2 3*4-1 3*4];        % bases empotradas
free  = setdiff(1:ndof, fixed);

% --- Ensamblaje de K y cargas nodales equivalentes de la distribuida ---
K = zeros(ndof, ndof);
F = zeros(ndof, 1);
for e = 1:nE
    NI = EL(e,2);  NJ = EL(e,3);
    Ee = EL(e,4);  Ae = EL(e,5);  Ie = EL(e,6);  w = EL(e,7);
    dx = XY(NJ,2)-XY(NI,2);  dy = XY(NJ,3)-XY(NI,3);
    L  = sqrt(dx^2+dy^2);    c = dx/L;  sn = dy/L;
    ea = Ee*Ae/L;   ei = Ee*Ie;
    kloc = [ ea, 0,0, -ea, 0,0
             0, 12*ei/L^3, 6*ei/L^2, 0, -12*ei/L^3, 6*ei/L^2
             0, 6*ei/L^2, 4*ei/L, 0, -6*ei/L^2, 2*ei/L
            -ea, 0,0, ea, 0,0
             0, -12*ei/L^3, -6*ei/L^2, 0, 12*ei/L^3, -6*ei/L^2
             0, 6*ei/L^2, 2*ei/L, 0, -6*ei/L^2, 4*ei/L ];
    T = [ c sn 0 0 0 0; -sn c 0 0 0 0; 0 0 1 0 0 0
          0 0 0 c sn 0; 0 0 0 -sn c 0; 0 0 0 0 0 1 ];
    kg = T' * kloc * T;
    g  = [3*NI-2, 3*NI-1, 3*NI, 3*NJ-2, 3*NJ-1, 3*NJ];
    K(g,g) = K(g,g) + kg;
    % Fuerzas de empotramiento perfecto (local) para carga uniforme w hacia abajo
    if w ~= 0
        Qf = [0; w*L/2; w*L^2/12; 0; w*L/2; -w*L^2/12];
        F(g) = F(g) - T' * Qf;          % carga nodal equivalente = -T'*Qf
    end
end

% --- Solucion  K u = F ---
u = zeros(ndof,1);
u(free) = inv(K(free,free)) * F(free);

% --- Fuerzas de extremo reales por elemento (local) -------------------
disp('=== Fuerzas de extremo por elemento (local: N, V, M) ===')
for e = 1:nE
    NI = EL(e,2);  NJ = EL(e,3);
    Ee = EL(e,4);  Ae = EL(e,5);  Ie = EL(e,6);  w = EL(e,7);
    dx = XY(NJ,2)-XY(NI,2);  dy = XY(NJ,3)-XY(NI,3);
    L  = sqrt(dx^2+dy^2);    c = dx/L;  sn = dy/L;
    ea = Ee*Ae/L;   ei = Ee*Ie;
    kloc = [ ea, 0,0, -ea, 0,0
             0, 12*ei/L^3, 6*ei/L^2, 0, -12*ei/L^3, 6*ei/L^2
             0, 6*ei/L^2, 4*ei/L, 0, -6*ei/L^2, 2*ei/L
            -ea, 0,0, ea, 0,0
             0, -12*ei/L^3, -6*ei/L^2, 0, 12*ei/L^3, -6*ei/L^2
             0, 6*ei/L^2, 2*ei/L, 0, -6*ei/L^2, 4*ei/L ];
    T = [ c sn 0 0 0 0; -sn c 0 0 0 0; 0 0 1 0 0 0
          0 0 0 c sn 0; 0 0 0 -sn c 0; 0 0 0 0 0 1 ];
    g  = [3*NI-2, 3*NI-1, 3*NI, 3*NJ-2, 3*NJ-1, 3*NJ];
    fl = kloc * (T * u(g));
    if w ~= 0
        fl = fl + [0; w*L/2; w*L^2/12; 0; w*L/2; -w*L^2/12];   % + empotramiento
        Mmid = -fl(3) + fl(2)*L/2 - w*(L/2)^2/2;              % momento de vano
        fprintf('Elem %d:  Ni=%7.2f Vi=%7.2f Mi=%7.2f | Vj=%7.2f Mj=%7.2f | M_vano=%7.2f\n', ...
                e, fl(1), fl(2), fl(3), fl(5), fl(6), Mmid);
    else
        fprintf('Elem %d:  Ni=%7.2f Vi=%7.2f Mi=%7.2f | Vj=%7.2f Mj=%7.2f\n', ...
                e, fl(1), fl(2), fl(3), fl(5), fl(6));
    end
end
R = K(fixed,:) * u - F(fixed);
disp('=== Reacciones en las bases (Fx,Fy,M) — ΣFy debe = w*Lviga = 120 kN ===')
disp(R')

% --- Grafico: portico + diagrama de momentos (viga: parabola) --------
figure; hold on; grid on; axis equal
esc = 0.008;
for e = 1:nE
    NI = EL(e,2);  NJ = EL(e,3);
    xi=XY(NI,2); yi=XY(NI,3); xj=XY(NJ,2); yj=XY(NJ,3);
    dx=xj-xi; dy=yj-yi; L=sqrt(dx^2+dy^2); c=dx/L; sn=dy/L; nx=-sn; ny=c;
    Ee=EL(e,4); Ie=EL(e,6); w=EL(e,7); ei=Ee*Ie;
    T = [ c sn 0 0 0 0; -sn c 0 0 0 0; 0 0 1 0 0 0
          0 0 0 c sn 0; 0 0 0 -sn c 0; 0 0 0 0 0 1 ];
    g = [3*NI-2, 3*NI-1, 3*NI, 3*NJ-2, 3*NJ-1, 3*NJ];
    ul = T * u(g);
    plot([xi xj],[yi yj],'-k','LineWidth',2.5);
    ns = 12;   Mx = zeros(1,ns+1);   px = zeros(1,ns+1);   py = zeros(1,ns+1);
    for kk = 0:ns
        xl = kk*L/ns;
        % momento por interpolacion Hermite de la solucion + parte de carga
        M = ei*( (6*ul(2) - 4*L*ul(3) - 6*ul(5) - 2*L*ul(6))/L^2 ...
                 + (12*ul(2) + 6*L*ul(3) - 12*ul(5) + 6*L*ul(6))/L^3 * (-xl) ) ;
        M = -M;
        if w ~= 0, M = M + w*xl*(L-xl)/2; end   % + parabola simple-apoyada
        Mx(kk+1) = M;   px(kk+1) = xi + c*xl + esc*M*nx;   py(kk+1) = yi + sn*xl + esc*M*ny;
    end
    plot(px, py, '-b', 'LineWidth', 1.3);
    plot([xi px(1)],[yi py(1)],'-b');   plot([xj px(end)],[yj py(end)],'-b');
end
plot(XY(:,2),XY(:,3),'ko','MarkerFaceColor','k','MarkerSize',7);
title('Portico con carga distribuida (UCAS) — negro: estructura, azul: momentos');
xlabel('x (m)');  ylabel('y (m)');
