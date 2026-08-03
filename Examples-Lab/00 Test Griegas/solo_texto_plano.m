% #md
% # Salida en TEXTO PLANO
% Con `% #plain` la salida de disp/fprintf queda literal (sin renderizar).
% #endmd

% #plain
disp('=== Reporte en texto plano ===')
fprintf('Modulo E   = 35000 MPa\n')
fprintf('Poisson nu = 0.15\n')
fprintf('sigma = 250 MPa,   x^2 = 9\n')
fprintf('theta = 30 grados, area = 25 m^2\n')
fprintf('alpha beta gamma delta\n')
disp('Nombres, letras griegas y ^N quedan sin transformar.')
