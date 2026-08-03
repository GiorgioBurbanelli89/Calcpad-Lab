%% Test de griegas y render de disp (toggles nuevos)
%-- El codigo va SIEMPRE en ASCII (nu, phi, xi) -> valido en MATLAB real.
%-- El OUTPUT muestra las griegas por defecto. Se controla por bloque e inline.

%% 1) Griegas por defecto ON (dentro de #noc)
nu = 0.15;
E  = 35000;
% #noc D = E*t^3/(12*(1 - nu^2))*[1; nu; 0|nu; 1; 0|0; 0; (1 - nu)/2]
% #noc phi_1(x) = 1 - x^2*(3 - 2*x)

%% 2) INLINE: solo esta linea en texto plano (nu queda literal)
% #nogreek #noc D_plano = E/(1 - nu^2)

%% 3) BLOQUE en texto plano
% #nogreek
% #noc sigma = E*nu
% #noc tau = phi*psi
% #greek

%% 4) fprintf: por defecto PLANO
phi = 20.63;
fprintf('phi = %.2f grados (plano)\n', phi);

%% 5) fprintf RENDERIZADO (bloque #render)
% #render
fprintf('phi = %.2f, nu = %.2f (renderizado)\n', phi, nu);
% #plain
fprintf('phi = %.2f (de nuevo plano)\n', phi);
