% ============================================================
%  Pandeo Lateral-Torsional (AISC 360 F2) - version compacta
%  Seccion I soldada 32x12 cm, acero A36.
%  Unidades consistentes + validacion "con unidades" vs
%  "sin unidades" (mismo sistema): ambas deben coincidir (dif ~ 0).
% ============================================================

% --- Material (entrada mixta: geometria cm, Fy ksi, E MPa) ---
E   = 200000;            % Modulo de elasticidad (MPa)
Fy  = 36.259 * 6.89476;  % Fy: 36.259 ksi -> MPa (~250 MPa)
Cb  = 1.0;               % Coeficiente de modificacion

% --- Seccion I soldada (cm) ---
d = 32; bf = 12; tf = 0.6; tw = 0.3;
A   = 2*bf*tf + (d - 2*tf)*tw;                                   % cm^2
Ix  = bf*d^3/12 - (bf - tw)*(d - 2*tf)^3/12;                     % cm^4
Iy  = 2*(tf*bf^3/12) + (d - 2*tf)*tw^3/12;                       % cm^4
Sx  = Ix/(d/2);                                                  % cm^3
Zx  = bf*tf*(d - tf) + tw*(d - 2*tf)^2/4;                        % cm^3
J   = 2*bf*tf^3/3 + (d + tf)*tw^3/3;                             % cm^4
ry  = sqrt(Iy/A);                                                % cm
ho  = d - tf;                                                    % cm
Cw  = Iy*ho^2/4;                                                 % cm^6
rts = sqrt(sqrt(Iy*Cw)/Sx);   % r_ts^2 = sqrt(Iy*Cw)/Sx         % cm

% ============================================================
%  VALIDACION: Way A (con conversiones)  vs  Way B (consistente)
% ============================================================
% ---- Way A: "con unidades", longitudes en cm ----
Lp_A = 1.76 * ry * sqrt(E/Fy);                                  % cm
t1_A = J/(Sx*ho);                                               % adimensional
t2_A = 6.76*(0.7*Fy/E)^2;                                       % adimensional
% AISC F2-6: UN sqrt EXTERIOR sobre  t1 + sqrt(t1^2 + t2)
Lr_A = 1.95 * rts * (E/(0.7*Fy)) * sqrt(t1_A + sqrt(t1_A^2 + t2_A));   % cm
Mp_A = Zx * Fy * 1e-3;                                          % kN*m  (cm^3*MPa*1e-3)

% ---- Way B: "sin unidades" (sistema N-mm-MPa, sin factores) ----
ry_mm = ry*10;  rts_mm = rts*10;  ho_mm = ho*10;               % cm -> mm
Sx_mm3 = Sx*1e3;  Zx_mm3 = Zx*1e3;  J_mm4 = J*1e4;            % cm^n -> mm^n
Lp_Bm = 1.76 * ry_mm * sqrt(E/Fy);
t1_B  = J_mm4/(Sx_mm3*ho_mm);
t2_B  = 6.76*(0.7*Fy/E)^2;
Lr_Bm = 1.95 * rts_mm * (E/(0.7*Fy)) * sqrt(t1_B + sqrt(t1_B^2 + t2_B));
Mp_Bn = Zx_mm3 * Fy;                                           % N*mm
Lp_B = Lp_Bm/10;  Lr_B = Lr_Bm/10;  Mp_B = Mp_Bn/1e6;        % -> cm, cm, kN*m

% ---- Resultados adoptados ----
Lp = Lp_A;  Lr = Lr_A;  Mp = Mp_A;
Mr = 0.7*Fy*Sx*1e-3;                                          % kN*m (momento en Lr)

disp('--- Validacion: con unidades vs sin unidades (mismo sistema) ---');
disp(['Lp: con = ', num2str(Lp_A), ' cm | sin = ', num2str(Lp_B), ' cm | dif = ', num2str(abs(Lp_A-Lp_B))]);
disp(['Lr: con = ', num2str(Lr_A), ' cm | sin = ', num2str(Lr_B), ' cm | dif = ', num2str(abs(Lr_A-Lr_B))]);
disp(['Mp: con = ', num2str(Mp_A), ' kN*m | sin = ', num2str(Mp_B), ' kN*m | dif = ', num2str(abs(Mp_A-Mp_B))]);
disp(['Chequeo fisico Lp < Lr : ', num2str(Lp < Lr)]);

% ============================================================
%  Curva M_n(L_b): tres regiones en orden Lp < Lr  (kN*m vs cm)
% ============================================================
Lb = linspace(1, 1.3*Lr, 1000);   % cm (desde 1: Fcr singular en Lb=0)
Mn1 = Mp * ones(size(Lb));
Mn2 = Cb * (Mp - (Mp - Mr) .* (Lb - Lp) / (Lr - Lp));
% Fcr: el sqrt MULTIPLICA (no divide). Fcr[MPa]*Sx[cm^3]*1e-3 -> kN*m
Fcr = Cb * pi^2 * E ./ (Lb/rts).^2 .* sqrt(1 + 0.078*(J/(Sx*ho)) * (Lb/rts).^2);
Mn3 = Fcr * Sx * 1e-3;

figure;
hold on;
plot(Lb(Lb <= Lp),               Mn1(Lb <= Lp),               'r', 'LineWidth', 1.5);
plot(Lb(Lb > Lp & Lb <= Lr),     Mn2(Lb > Lp & Lb <= Lr),     'g', 'LineWidth', 1.5);
plot(Lb(Lb > Lr),                Mn3(Lb > Lr),                'b', 'LineWidth', 1.5);
plot([Lp Lp], [0 Mp], 'k--');
plot([Lr Lr], [0 Mp], 'k--');
xlabel('Longitud no arriostrada, L_b (cm)');
ylabel('Resistencia nominal a la flexion, M_n (kN*m)');
title('Resistencia Nominal a la Flexion vs Longitud no Arriostrada');
text(Lp*0.15, Mp,          ' Plastic Design', 'Color', 'r');
text((Lp+Lr)/2, Mp*0.92,   ' Inelastic LTB',  'Color', [0 0.6 0]);
text(Lr*1.02, Mr*0.85,     ' Elastic LTB',    'Color', 'b');
text(Lp, 0, ' L_p', 'Color', 'k');
text(Lr, 0, ' L_r', 'Color', 'k');
grid on;
hold off;

disp('Plastic Design (Lb<=Lp): Mn=Mp. Inelastic LTB (Lp<Lb<=Lr): recta Mp->Mr. Elastic LTB (Lb>Lr): Mn=Fcr*Sx.');
