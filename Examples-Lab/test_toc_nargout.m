% Verifica fidelidad MATLAB de toc (nargout)
A = rand(400) + 400*eye(400);
b = rand(400,1);

% 1) toc COMANDO -> debe imprimir "Elapsed time is ..."
tic
x = A\b;
toc

% 2) t = toc;  -> NO debe imprimir nada
tic
x = A\b;
t = toc;
fprintf('guardado en variable: %.4f s\n', t)

% 3) toc(id) COMANDO -> debe imprimir
id = tic;
x = A\b;
toc(id)

% 4) t = toc(id);  -> NO debe imprimir
id = tic;
x = A\b;
t = toc(id);
fprintf('handle en variable: %.4f s\n', t)
