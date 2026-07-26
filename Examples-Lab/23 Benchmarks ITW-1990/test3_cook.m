% ============================================================================
%  TEST III - Membrana de Cook (elemento ITW-1990 con drilling)
%  Viga trapezoidal en voladizo a cortante. Test clasico de distorsion de malla.
%  Paper Tabla III (D-type): 1x1=14.065  2x2=20.682  4x4=22.984  8x8=23.626
%  Referencia (malla fina) = 23.91.
% ============================================================================
tic;
E = 1;  nu = 1/3;  t = 1;  V = 1;  n = 3;
for n_a = [1 2 4 8]                       % barrido de malla como el paper
    n_b = n_a;  n_e = n_a*n_b;  n_j = (n_a+1)*(n_b+1);
    % --- malla (trapecio de Cook) ---
    x_j = zeros(n_j,1);  z_j = zeros(n_j,1);
    for j_b = 0:n_b
        for i_a = 0:n_a
            k = j_b*(n_a+1) + i_a + 1;
            aa = i_a/n_a;  bb = j_b/n_b;
            x_j(k) = 48*aa;
            z_j(k) = 44*aa + 44*bb - 28*aa*bb;
        end
    end
    e_j = zeros(n_e,4);
    for j_b = 0:n_b-1
        for i_a = 0:n_a-1
            e = j_b*n_a + i_a + 1;
            e_j(e,1) = j_b*(n_a+1) + i_a + 1;
            e_j(e,2) = j_b*(n_a+1) + i_a + 2;
            e_j(e,3) = (j_b+1)*(n_a+1) + i_a + 2;
            e_j(e,4) = (j_b+1)*(n_a+1) + i_a + 1;
        end
    end
    D   = E*t/(1-nu^2) * [1 nu 0; nu 1 0; 0 0 (1-nu)/2];
    gam = E/(2*(1+nu));
    n_g = n*n_j;  K = zeros(n_g,n_g);
    for e = 1:n_e
        q = e_j(e,:)';
        K_e = ke_itw(x_j(q), z_j(q), D, gam, t);
        for i = 1:4
            for j = 1:4
                i1 = n*(e_j(e,i)-1);  i2 = n*(e_j(e,j)-1);
                K(i1+1:i1+3, i2+1:i2+3) = K(i1+1:i1+3, i2+1:i2+3) + ...
                    K_e(n*(i-1)+1:n*i, n*(j-1)+1:n*j);
            end
        end
    end
    % apoyo: borde izquierdo x=0 empotrado
    k_s = 1e12;
    for j = 1:n_j
        if x_j(j)==0
            d = n*(j-1);
            K(d+1,d+1)=K(d+1,d+1)+k_s; K(d+2,d+2)=K(d+2,d+2)+k_s; K(d+3,d+3)=K(d+3,d+3)+k_s;
        end
    end
    % carga cortante V en uz del borde libre x=48 (trapezoidal consistente)
    F = zeros(n_g,1);
    for j = 1:n_j
        if x_j(j)==48
            d = n*(j-1)+2;  w_l = 1/n_b;
            if z_j(j)==44 || z_j(j)==60, w_l = 1/(2*n_b); end
            F(d) = F(d) + V*w_l;
        end
    end
    Z = K\F;
    % flecha vertical del punto medio del borde libre (x=48, z=52)
    jtip = 0;
    for j = 1:n_j
        if x_j(j)==48 && abs(z_j(j)-52)<1e-9, jtip = j; end
    end
    if jtip==0   % malla impar: tomar el nudo del borde libre mas cercano a z=52
        best=1e9;
        for j=1:n_j
            if x_j(j)==48 && abs(z_j(j)-52)<best, best=abs(z_j(j)-52); jtip=j; end
        end
    end
    d_tip = abs(Z(n*(jtip-1)+2));
    fprintf('Cook %dx%d : delta_tip = %8.4f   (paper D-type ref)\n', n_a, n_b, d_tip);
    if n_a==8    % guardar campo para la grafica (malla 8x8)
        UZ=zeros(n_j,1); for j=1:n_j, UZ(j)=Z(n*(j-1)+2); end
        gxx=x_j; gzz=z_j; gna=n_a; gnb=n_b; guz=UZ;
    end
end
fprintf('referencia malla fina = 23.91\n');
fprintf('CHECK t_seg %.4f\n', toc);

% ---------- grafica: uz como contourf en el trapecio (bandas + isolineas) ----------
UM=reshape(guz, gna+1, gnb+1)';
XX=reshape(gxx, gna+1, gnb+1)';
ZZ=reshape(gzz, gna+1, gnb+1)';
figure;
contourf(XX, ZZ, UM, 16, 'LineColor', 'none'); hold on;
contour(XX, ZZ, UM, 16, 'LineWidth', 0.6);
colormap(jet); colorbar; axis equal;
xlabel('x [m]'); ylabel('z [m]');
title('Cook membrane ITW 8x8 - u_z');
hold off;
