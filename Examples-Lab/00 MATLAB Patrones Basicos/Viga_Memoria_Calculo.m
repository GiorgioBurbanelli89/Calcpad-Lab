% 03_Viga_Memoria_Calculo.m
% =========================================================================
% EJEMPLO 3 — Memoria de calculo estructural en estilo libro
% =========================================================================
% Caso practico: viga simplemente apoyada con carga uniforme.
% Demuestra como combinar prosa fluida + ecuaciones simbolicas + resultados
% numericos en un documento legible tipo libro de mecanica.
%
% Tecnicas usadas:
%   - syms, diff, int, simplify, subs para algebra simbolica
%   - char(expr) para insertar simbolico en fprintf con %s
%   - Caso numerico al final (subs encadenadas — Hekatan Lab MVP no
%     soporta cell arrays en subs)
%
% Ejecutar:
%   - MATLAB: F5
%   - Hekatan Lab CLI: HekatanLabCli.exe 03_Viga_Memoria_Calculo.m
% =========================================================================

clear; clc;
syms q L x E I sigma_adm

% --- Reacciones ---
RA = q*L/2;
M_x = simplify(RA*x - q*x^2/2);
V_x = RA - q*x;

fprintf('Consideremos una viga simplemente apoyada de luz L sometida a una carga\n');
fprintf('uniformemente distribuida q por unidad de longitud. Por la simetria del\n');
fprintf('sistema, las reacciones verticales en ambos apoyos son iguales y valen\n');
fprintf('R_A = R_B = %s, repartiendo la carga total q*L entre los dos puntos\n', char(RA));
fprintf('de apoyo.\n\n');

fprintf('El esfuerzo cortante varia linealmente a lo largo de la viga, partiendo\n');
fprintf('del valor maximo positivo en el apoyo izquierdo y descendiendo hasta el\n');
fprintf('valor negativo de igual magnitud en el apoyo derecho. Su expresion es\n');
fprintf('V(x) = %s, que se anula exactamente en el centro de la luz, en x = L/2.\n\n', char(V_x));

% --- Momento maximo ---
M_max = simplify(subs(M_x, x, L/2));

fprintf('El momento flector, obtenido al integrar el cortante respecto a x, sigue\n');
fprintf('una ley parabolica con concavidad hacia arriba. Tomamos por convencion\n');
fprintf('positivo el momento que produce traccion en la fibra inferior. La\n');
fprintf('expresion analitica es M(x) = %s, que alcanza su valor maximo justo en\n', char(M_x));
fprintf('el punto donde el cortante se anula, es decir, en x = L/2. Sustituyendo\n');
fprintf('obtenemos el momento maximo M_max = %s, cantidad que define el\n', char(M_max));
fprintf('dimensionado a flexion de la viga.\n\n');

% --- Deflexion ---
yp1 = int(M_x/(E*I), x);
delta_max_sym = -5*q*L^4/(384*E*I);

fprintf('La deflexion de la viga se obtiene mediante doble integracion de la\n');
fprintf('ecuacion diferencial de la elastica E*I*y''''(x) = M(x). La primera\n');
fprintf('integracion produce E*I*y''(x) = %s + C1, donde C1 es una constante\n', char(yp1*E*I));
fprintf('de integracion a determinar. Una segunda integracion, junto con las\n');
fprintf('condiciones de contorno y(0) = y(L) = 0 propias de los apoyos simples,\n');
fprintf('conduce despues de algebra elemental al resultado clasico para la\n');
fprintf('flecha en el centro: delta_max = %s. El signo negativo indica que el\n', char(delta_max_sym));
fprintf('desplazamiento se produce hacia abajo, en el sentido de la carga.\n\n');

% --- Modulo resistente ---
W_req = simplify(M_max/sigma_adm);

fprintf('Para el dimensionado a flexion segun el criterio de tensiones admisibles,\n');
fprintf('imponemos que la tension maxima sigma = M_max/W no supere la tension\n');
fprintf('admisible del material sigma_adm. Despejando, el modulo resistente\n');
fprintf('minimo requerido es W_req = M_max/sigma_adm = %s. Cualquier perfil\n', char(W_req));
fprintf('comercial con un W superior a este valor satisface la condicion de\n');
fprintf('resistencia.\n\n');

% --- Caso numerico ---
M_max_n = subs(M_max, q, 15e3); M_max_n = subs(M_max_n, L, 6);
W_req_n = subs(W_req, q, 15e3); W_req_n = subs(W_req_n, L, 6); W_req_n = subs(W_req_n, sigma_adm, 235e6);
M_max_num = double(M_max_n);
W_req_num = double(W_req_n);

I_IPE300 = 8.36e-5;
d_n = subs(delta_max_sym, q, 15e3); d_n = subs(d_n, L, 6); d_n = subs(d_n, E, 210e9); d_n = subs(d_n, I, I_IPE300);
delta_num = abs(double(d_n));

fprintf('Apliquemos estas formulas a un caso practico: una viga de luz L = 6 m que\n');
fprintf('soporta una carga uniforme q = 15 kN/m, ejecutada en acero S235 con\n');
fprintf('sigma_adm = 235 MPa y modulo elastico E = 210 GPa. El momento maximo\n');
fprintf('resulta M_max = %.2f kN*m, valor que exige un modulo resistente minimo\n', M_max_num/1e3);
fprintf('de W_req = %.2f cm^3. Un perfil IPE 300, cuyo modulo W es del orden de\n', W_req_num*1e6);
fprintf('557 cm^3, satisface holgadamente la verificacion a resistencia.\n\n');

fprintf('Verifiquemos finalmente la deformacion. Con la inercia del IPE 300\n');
fprintf('I = 8.36e-5 m^4, la flecha maxima en el centro de la luz vale\n');
fprintf('delta_max = %.2f mm, lo que equivale a una relacion luz/flecha de L/%.0f.\n', delta_num*1000, 6/delta_num);
fprintf('Comparado con el limite habitual de L/300 para vigas de piso, la viga\n');
fprintf('cumple con margen el criterio de servicio.\n');
