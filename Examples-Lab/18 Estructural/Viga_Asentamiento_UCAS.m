% ============================================================
%  VIGA CONTINUA con ASENTAMIENTO DE APOYO  (metodo UCAS 2D)
%  UCAS permite prescribir desplazamientos nodales (Asignar_
%  Desplazamientos_Nodales). Aqui el apoyo CENTRAL de una viga
%  continua de 2 vanos se asienta un valor D. Sin carga externa,
%  el asentamiento diferencial genera MOMENTOS internos.
%  Solucion con GDL prescritos:  Kff*uf = -Kfp*up.
% ============================================================

% Nodos: [ id  x  y ]  (m)  — viga horizontal, 2 vanos iguales
XY = [ 1  0  0
       2  4  0
       3  8  0 ];
% Elementos: [ id  nI  nJ   E(kPa)   A(m2)   I(m4) ]
EL = [ 1  1  2   200e6   0.01   1.0e-4
       2  2  3   200e6   0.01   1.0e-4 ];

Delta = 0.010;              % asentamiento del apoyo central (m, hacia abajo)

nN = size(XY,1);  nE = size(EL,1);  ndof = 3*nN;

% GDL: por nodo [ux, uy, giro]. Apoyos simples: uy restringido, giro LIBRE.
% ux restringido en todos (sin axial). El apoyo central (nodo 2) tiene uy
% PRESCRITO = -Delta (se asienta).
prescribed = [1 2, 4 5, 7 8];              % ux1,uy1, ux2,uy2, ux3,uy3
up_val     = [0 0, 0 -Delta, 0 0];         % valores prescritos
free       = setdiff(1:ndof, prescribed);  % giros: 3, 6, 9

% --- Ensamblaje de K (elemento frame, sin carga externa) --------------
K = zeros(ndof, ndof);
for e = 1:nE
    NI = EL(e,2);  NJ = EL(e,3);
    Ee = EL(e,4);  Ae = EL(e,5);  Ie = EL(e,6);
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
end

% --- Solucion con desplazamientos prescritos:  Kff*uf = -Kfp*up -------
u = zeros(ndof,1);
u(prescribed) = up_val';
Kff = K(free,free);
Kfp = K(free,prescribed);
u(free) = inv(Kff) * ( -Kfp * up_val' );

% --- Momentos de extremo por elemento ---------------------------------
fprintf('Asentamiento del apoyo central: D = %g m (%g mm)\n', Delta, Delta*1000);
disp('=== Momentos de extremo por elemento (kN*m) ===')
for e = 1:nE
    NI = EL(e,2);  NJ = EL(e,3);
    Ee = EL(e,4);  Ae = EL(e,5);  Ie = EL(e,6);
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
    fprintf('Elem %d (%d-%d):  Mi = %8.3f   Mj = %8.3f   (V = %7.3f kN)\n', ...
            e, EL(e,2), EL(e,3), fl(3), fl(6), fl(2));
end
R = K(prescribed,:) * u;
disp('=== Reacciones verticales en apoyos (kN) — deben sumar ~0 ===')
fprintf('Apoyo 1: %8.3f   Apoyo 2 (asentado): %8.3f   Apoyo 3: %8.3f\n', R(2), R(4), R(6));
fprintf('Suma = %8.4f kN  (esperado ~0, sin carga externa)\n', R(2)+R(4)+R(6));

% Momento teorico sobre el apoyo central (viga 2 vanos iguales, asent. central):
Mteo = 3*EL(1,4)*EL(1,6)*Delta / (4^2);   % 3EI*D/L^2
fprintf('Momento teorico sobre apoyo central (3EI*D/L^2) = %.3f kN*m\n', Mteo);

% --- Grafico: viga original + deformada por asentamiento --------------
figure; hold on; grid on
esc = 30;
plot(XY(:,2), XY(:,3), '-k', 'LineWidth', 2.5);
xd = XY(:,2)';   yd = [u(2) u(5) u(8)]*esc;
% deformada suave por vano (muestreo Hermite)
for e = 1:nE
    NI = EL(e,2); NJ = EL(e,3); L = XY(NJ,2)-XY(NI,2);
    vi=u(3*NI-1); ti=u(3*NI); vj=u(3*NJ-1); tj=u(3*NJ);
    ns=16; xx=zeros(1,ns+1); yy=zeros(1,ns+1);
    for kk=0:ns
        xl=kk*L/ns; s=xl/L;
        N1=1-3*s^2+2*s^3; N2=L*(s-2*s^2+s^3); N3=3*s^2-2*s^3; N4=L*(-s^2+s^3);
        v=N1*vi+N2*ti+N3*vj+N4*tj;
        xx(kk+1)=XY(NI,2)+xl; yy(kk+1)=esc*v;
    end
    plot(xx,yy,'--r','LineWidth',1.5);
end
plot(XY(:,2), yd, 'ro', 'MarkerFaceColor','r','MarkerSize',6);
title(sprintf('Viga continua con asentamiento central D=%gmm (UCAS) — rojo: deformada x%g', Delta*1000, esc));
xlabel('x (m)');  ylabel('desplazamiento (m, escalado)');
