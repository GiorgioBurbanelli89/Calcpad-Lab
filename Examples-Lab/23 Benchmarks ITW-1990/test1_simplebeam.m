% ============================================================================
%  TEST I - Viga simple (patch test de orden superior: FLEXION PURA)
%  Relacion l/h = 10.  E=100, nu=0, l=10, h=1, 6 elementos en una fila.
%  Cupla unitaria (M=1) en ambos extremos -> flexion pura constante.
%  Beam theory exacto: v_mid = M l^2/(8EI) = 1.5 ,  rot_extremo = M l/(2EI) = 0.6
%  (rot = GDL drilling rz en el nudo extremo -> prueba directa del drilling)
% ============================================================================
tic;
E = 100;  nu = 0;  l = 10;  h = 1;  n = 3;  M = 1;
n_x = 6;  n_y = 1;  n_e = n_x*n_y;  n_j = (n_x+1)*(n_y+1);
x_j = zeros(n_j,1);  y_j = zeros(n_j,1);
for j = 0:n_y
    for i = 0:n_x
        k=j*(n_x+1)+i+1; x_j(k)=i*l/n_x; y_j(k)=j*h/n_y;
    end
end
e_j = zeros(n_e,4);
for j = 0:n_y-1
    for i = 0:n_x-1
        e=j*n_x+i+1;
        e_j(e,1)=j*(n_x+1)+i+1; e_j(e,2)=j*(n_x+1)+i+2;
        e_j(e,3)=(j+1)*(n_x+1)+i+2; e_j(e,4)=(j+1)*(n_x+1)+i+1;
    end
end
D   = E/(1-nu^2) * [1 nu 0; nu 1 0; 0 0 (1-nu)/2];
gam = E/(2*(1+nu));
K = assemble_itw(x_j, y_j, e_j, D, gam, 1);
% restricciones minimas (3): quitar cuerpo rigido; NINGUN drilling restringido
nBL = 1;                 % (0,0) esquina inf-izq
nBR = n_x+1;             % (l,0) esquina inf-der
nTL = (n_x+1)+1;         % (0,h) esquina sup-izq
k_s = 1e12;
K(n*(nBL-1)+1,n*(nBL-1)+1) = K(n*(nBL-1)+1,n*(nBL-1)+1) + k_s;   % ux izq
K(n*(nBL-1)+2,n*(nBL-1)+2) = K(n*(nBL-1)+2,n*(nBL-1)+2) + k_s;   % uy izq
K(n*(nBR-1)+2,n*(nBR-1)+2) = K(n*(nBR-1)+2,n*(nBR-1)+2) + k_s;   % uy der (rodillo)
% cuplas de flexion pura: fuerzas +-F=M/h en las caras extremas
F = zeros(n*n_j,1);  Ff = M/h;
% extremo derecho (x=l): +F arriba, -F abajo
F(n*(nBR-1)+1)               = F(n*(nBR-1)+1)               - Ff;   % inf-der
F(n*((n_x+1)+n_x+1-1)+1)     = F(n*((n_x+1)+n_x+1-1)+1)     + Ff;   % sup-der
% extremo izquierdo (x=0): -F arriba, +F abajo
F(n*(nBL-1)+1)               = F(n*(nBL-1)+1)               + Ff;   % inf-izq
F(n*(nTL-1)+1)               = F(n*(nTL-1)+1)               - Ff;   % sup-izq
Z = K\F;
% v en el centro (x=l/2, y=0) ; rot = rz en el nudo extremo (x=l,y=0)
jm = 0; for j=1:n_j, if abs(x_j(j)-l/2)<1e-9 && y_j(j)==0, jm=j; end, end
v_mid = abs(Z(n*(jm-1)+2));
rot_e = abs(Z(n*(nBR-1)+3));
t_seg = toc;
fprintf('==== TEST I SIMPLE BEAM (flexion pura, ITW) ====\n');
fprintf('v_mid (centro)   = %.5f   (exacto 1.5)\n', v_mid);
fprintf('rot extremo (rz) = %.5f   (exacto 0.6)\n', rot_e);
fprintf('CHECK t_seg %.4f\n', t_seg);

% ---------- grafica: desplazamiento vertical uy (contourf estilo deep beam) ----------
UY = zeros(n_j,1); for j=1:n_j, UY(j)=Z(n*(j-1)+2); end
xv=(0:n_x)*l/n_x; yv=(0:n_y)*h/n_y;
UM=reshape(UY,n_x+1,n_y+1)'; [Xg,Yg]=meshgrid(xv,yv);
xf=linspace(0,l,240); yf=linspace(0,h,24); [Xf,Yf]=meshgrid(xf,yf);
UMf=interp2(Xg,Yg,UM,Xf,Yf,'linear');
figure;
contourf(Xf,Yf,UMf,16,'LineColor','none'); hold on;
contour(Xf,Yf,UMf,16,'LineWidth',0.6);
colormap(jet); colorbar; axis equal; axis([0 l 0 h]);
xlabel('x [m]'); ylabel('y [m]');
title('Simple beam ITW - flexion pura, u_y');
hold off;
