%' # Formulacion de un talud por elementos finitos — como lo hace GEO5 2024
%'
%' Todo lo que sigue esta MEDIDO contra el GEO5 2024 instalado (modulo MEF de
%' estabilidad de taludes), no deducido de la documentacion. El talud de
%' referencia es la Demo04 de GEO5, 3 etapas, que en Hekatan Lab da FS = 1.69 /
%' 1.48 / 1.69 (gravedad / +sobrecarga 35 kPa / +ancla 72 kN) — igual que GEO5.

%' ## 1. El material NO es elastico: es elasto-plastico
%'
%' GEO5 usa el criterio de Drucker-Prager (no Mohr-Coulomb clasico: se
%' comprobo leyendo el binario del solver — la funcion de fluencia usa los
%' invariantes de tension, no las tensiones principales). En deformacion plana,
%' la relacion elastica es la matriz constitutiva:

% #noc D = E/((1 + ν)*(1 - 2*ν)) * [1 - ν; ν; 0 | ν; 1 - ν; 0 | 0; 0; (1 - 2*ν)/2]

%' ## 2. Invariantes de tension
%'
%' El estado de tension se describe por dos invariantes: el primero (presion
%' media) y el segundo del desviador. Los deduzco simbolico.
syms sx sy sz txy real
I1 = sx + sy + sz;                       % primer invariante (traza)
p  = I1/3;                               % presion media
dev = [sx - p; sy - p; sz - p; txy];     % tensor desviador (parte que corta)
J2  = simplify(1/2*(dev(1)^2 + dev(2)^2 + dev(3)^2) + dev(4)^2);
disp('Primer invariante  I_1 = sx + sy + sz ='),      disp(I1)
disp('Segundo invariante del desviador  J_2 ='),      disp(J2)

%' ## 3. La superficie de fluencia de Drucker-Prager
%'
%' El suelo fluye (empieza a fallar) cuando la tension cortante generalizada
%' iguala a la resistencia. La funcion de fluencia de GEO5 es:

% #noc f = sqrt(J_2) + alpha*I_1 - k

%' con f < 0 elastico, f = 0 en fluencia. Los parametros alpha y k salen de la
%' cohesion c y el angulo de friccion phi (cono de Drucker-Prager INSCRITO en
%' Mohr-Coulomb en compresion, que es lo que replica GEO5):

% #noc alpha = 2*sin(phi) / (sqrt(3)*(3 + sin(phi)))
% #noc k = 6*c*cos(phi) / (sqrt(3)*(3 + sin(phi)))

%' Comprobacion numerica con los parametros del suelo 1 de la Demo04
%' (c = 8 kPa, phi = 29 grados):
phi = 29*pi/180;   c = 8;
alpha = 2*sin(phi)/(sqrt(3)*(3 + sin(phi)));
k     = 6*c*cos(phi)/(sqrt(3)*(3 + sin(phi)));
fprintf('alpha = %.4f ,  k = %.3f kPa\n', alpha, k);

%' ## 4. Reduccion de resistencia (SRM) — de donde sale el Factor de Seguridad
%'
%' GEO5 NO busca una superficie de falla: reduce la resistencia del suelo por un
%' factor F hasta que el modelo deja de converger (el talud "falla"). Ese F de
%' falla ES el factor de seguridad. La resistencia reducida:
syms cc phic F real
c_F   = cc/F;                            % cohesion reducida
% #noc c_F = c / F
% #noc phi_F = atan( tan(phi) / F )
phi_F = atan(tan(phic)/F);               % friccion reducida (la ley CORRECTA: atan)
disp('Cohesion reducida  c_F ='),   disp(c_F)
disp('Friccion reducida  phi_F ='), disp(phi_F)

%' Cuando F = 1 se usa la resistencia real (talud tal cual). Al ir subiendo F,
%' c_F y phi_F bajan; en F = FS el sistema no converge -> ESE es el resultado.

%' ## Resumen de la formulacion
%'
%' - Material elasto-plastico Drucker-Prager (no elastico, no MC principal).
%' - Fluencia por invariantes:  f = sqrt(J_2) + alpha*I_1 - k.
%' - alpha, k de c y phi (cono inscrito en compresion).
%' - El FS sale de reducir c y phi por F hasta la NO convergencia (SRM), con la
%'   ley de reduccion de friccion en atan(tan(phi)/F).
%' - Rigidez inicial elastica + return-mapping (sub-stepping) por punto de Gauss.
