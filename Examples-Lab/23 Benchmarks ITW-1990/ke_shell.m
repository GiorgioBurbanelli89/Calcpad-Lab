function [Kg, Tt] = ke_shell(XYZ, E, nu, t)
% Elemento SHELL plano cuadrilatero = membrana ITW (u,v,thz) + placa DKQ (w,thx,thy).
% 4 nudos x 6 GDL globales [u v w thx thy thz] = 24x24.
% XYZ = 4x3 coords globales de los nudos.  Devuelve Kg (24x24) y T (24x24).
X = XYZ(:,1);  Y = XYZ(:,2);  Z = XYZ(:,3);
% --- ejes locales del elemento (plano medio) ---
v1 = [X(2)-X(1); Y(2)-Y(1); Z(2)-Z(1)];      % borde 1->2
v2 = [X(4)-X(1); Y(4)-Y(1); Z(4)-Z(1)];      % borde 1->4
e3 = cross(v1, v2);  e3 = e3(:)/norm(e3);     % normal (cross en Lab da fila -> forzar col)
e1 = v1/norm(v1);                             % eje local x
e1 = e1 - (e1'*e3)*e3;  e1 = e1/norm(e1);     % ortogonalizar
e2 = cross(e3, e1);  e2 = e2(:);              % eje local y
R  = [e1'; e2'; e3'];                         % global -> local (filas = ejes)
% --- coords locales 2D (plano del elemento) ---
xl = zeros(4,1);  yl = zeros(4,1);
for i = 1:4
    p = R*[X(i)-X(1); Y(i)-Y(1); Z(i)-Z(1)];
    xl(i) = p(1);  yl(i) = p(2);              % p(3)~0 (plano)
end
% --- rigideces locales ---
Dm  = E*t/(1-nu^2) * [1 nu 0; nu 1 0; 0 0 (1-nu)/2];
gam = E/(2*(1+nu));
Km  = ke_itw_g(xl, yl, Dm, gam, t, 2);        % membrana integracion REDUCIDA 2x2 (anti-locking Taylor) (anti-locking)
Kb  = ke_dkq(xl, yl, E, nu, t);               % 12x12 en [w thx thy] por nudo
% --- ensamblar en local 24x24 (orden por nudo: u v w thx thy thz) ---
Kl = zeros(24,24);
mm = [1 2 6];   % GDL de membrana dentro del bloque de 6 (u,v,thz)
pp = [3 4 5];   % GDL de placa (w,thx,thy)
for i = 1:4
    for j = 1:4
        bi = 6*(i-1);  bj = 6*(j-1);
        Kl(bi+mm, bj+mm) = Kl(bi+mm, bj+mm) + Km(3*(i-1)+1:3*i, 3*(j-1)+1:3*j);
        Kl(bi+pp, bj+pp) = Kl(bi+pp, bj+pp) + Kb(3*(i-1)+1:3*i, 3*(j-1)+1:3*j);
    end
end
% --- rigidez ficticia de drilling fuera-de-plano para evitar singularidad (thz global) ---
% (se agrega despues via el ensamblaje; aqui solo local membrana/placa)
% --- transformacion local->global: por nudo [R 0; 0 R] ---
Tt = zeros(24,24);
for i = 1:4
    b = 6*(i-1);
    Tt(b+1:b+3, b+1:b+3) = R;    % traslaciones u,v,w
    Tt(b+4:b+6, b+4:b+6) = R;    % rotaciones thx,thy,thz
end
Kg = Tt'*Kl*Tt;
end
