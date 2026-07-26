% ============================================================================
%  TEST II - Viga corta en voladizo (MacNeal-Harder) - elemento ITW-1990
%  E=30000, nu=0.25, P=40, l=48, h=12.  Empotrada izq, cortante P en el borde libre.
%  Paper Tabla II (D-type): 4x1=0.3445  8x2=0.3504  16x4=0.3543 ; exacto=0.3553
% ============================================================================
tic;
E = 30000;  nu = 0.25;  P = 40;  l = 48;  h = 12;  n = 3;
D   = E/(1-nu^2) * [1 nu 0; nu 1 0; 0 0 (1-nu)/2];   % t=1
gam = E/(2*(1+nu));
meshes = [4 1; 8 2; 16 4];
fprintf('==== TEST II CANTILEVER (ITW) ====  exacto = 0.3553\n');
for m = 1:size(meshes,1)
    n_x = meshes(m,1);  n_y = meshes(m,2);
    n_e = n_x*n_y;  n_j = (n_x+1)*(n_y+1);
    x_j = zeros(n_j,1);  y_j = zeros(n_j,1);
    for j = 0:n_y
        for i = 0:n_x
            k = j*(n_x+1)+i+1;  x_j(k) = i*l/n_x;  y_j(k) = j*h/n_y;
        end
    end
    e_j = zeros(n_e,4);
    for j = 0:n_y-1
        for i = 0:n_x-1
            e = j*n_x+i+1;
            e_j(e,1)=j*(n_x+1)+i+1; e_j(e,2)=j*(n_x+1)+i+2;
            e_j(e,3)=(j+1)*(n_x+1)+i+2; e_j(e,4)=(j+1)*(n_x+1)+i+1;
        end
    end
    K = assemble_itw(x_j, y_j, e_j, D, gam, 1);
    k_s = 1e12;                                   % empotrado x=0
    for j = 1:n_j
        if x_j(j)==0
            d=n*(j-1); K(d+1,d+1)=K(d+1,d+1)+k_s; K(d+2,d+2)=K(d+2,d+2)+k_s; K(d+3,d+3)=K(d+3,d+3)+k_s;
        end
    end
    F = zeros(n*n_j,1);                           % cortante P en uy del borde libre x=l
    for j = 1:n_j
        if x_j(j)==l
            w = P/n_y;  if y_j(j)==0 || y_j(j)==h, w = P/(2*n_y); end
            F(n*(j-1)+2) = F(n*(j-1)+2) + w;
        end
    end
    Z = K\F;
    % flecha en el punto medio del borde libre (x=l, y=h/2)
    jt = 0; best=1e9;
    for j=1:n_j, if x_j(j)==l && abs(y_j(j)-h/2)<best, best=abs(y_j(j)-h/2); jt=j; end, end
    d_tip = abs(Z(n*(jt-1)+2));
    fprintf('  %2dx%-2d : tip = %.4f\n', n_x, n_y, d_tip);
    if n_x==16   % guardar campos para la grafica
        UY = zeros(n_j,1);
        for j=1:n_j, UY(j) = Z(n*(j-1)+2); end
        gx=x_j; gy=y_j; gnx=n_x; gny=n_y; gum=UY; gej=e_j;
    end
end
% ---------- malla IRREGULAR 4x1* (Fig 4: arriba 12,12,12,12 ; abajo 16,4,8,20) ----------
xtop = [0 12 24 36 48];  xbot = [0 16 20 28 48];
xi_j = [xbot xtop]';                 % 5 abajo (y=0) + 5 arriba (y=h)
yi_j = [zeros(5,1); h*ones(5,1)];
ei_j = zeros(4,4);
for i = 1:4
    ei_j(i,1)=i; ei_j(i,2)=i+1; ei_j(i,3)=5+i+1; ei_j(i,4)=5+i;
end
Ki = assemble_itw(xi_j, yi_j, ei_j, D, gam, 1);
nji = 10;
for j = 1:nji
    if xi_j(j)==0
        d=n*(j-1); Ki(d+1,d+1)=Ki(d+1,d+1)+1e12; Ki(d+2,d+2)=Ki(d+2,d+2)+1e12; Ki(d+3,d+3)=Ki(d+3,d+3)+1e12;
    end
end
Fi = zeros(n*nji,1);                 % borde libre = 2 nudos (esquinas), P/2 c/u
for j = 1:nji
    if xi_j(j)==l
        Fi(n*(j-1)+2) = Fi(n*(j-1)+2) + P/2;
    end
end
Zi = Ki\Fi;
jt=0; best=1e9; for j=1:nji, if xi_j(j)==l && abs(yi_j(j)-h/2)<best, best=abs(yi_j(j)-h/2); jt=j; end, end
% no hay nudo en y=h/2 (solo y=0 e y=h) -> promedio de los dos del borde libre
d_irr = (abs(Zi(n*(5-1)+2)) + abs(Zi(n*(10-1)+2)))/2;
fprintf('  4x1* irregular : tip = %.4f   (paper D-type 0.3065)\n', d_irr);
fprintf('CHECK t_seg %.4f\n', toc);

% ---------- grafica: uy como contourf (bandas + isolineas, estilo deep beam) ----------
xv = (0:gnx)*l/gnx;  yv = (0:gny)*h/gny;
UM = reshape(gum, gnx+1, gny+1)';
[Xg,Yg]=meshgrid(xv,yv);
xf=linspace(0,l,200); yf=linspace(0,h,60); [Xf,Yf]=meshgrid(xf,yf);
UMf = interp2(Xg,Yg,UM,Xf,Yf,'linear');
figure;
contourf(Xf,Yf,UMf,16,'LineColor','none'); hold on;
contour(Xf,Yf,UMf,16,'LineWidth',0.6);
colormap(jet); colorbar; axis equal; axis([0 l 0 h]);
xlabel('x [m]'); ylabel('y [m]');
title('Cantilever ITW 16x4 - desplazamiento vertical u_y');
hold off;
