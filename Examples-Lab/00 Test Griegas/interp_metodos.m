%% Comparacion de metodos de interp2: linear vs cubic vs spline (malla gruesa -> fina)
xg = linspace(0,6,7); yg = linspace(0,4,5);
[Xg,Yg] = meshgrid(xg,yg);
Z = 10*exp(-((Xg-3).^2/4 + (Yg-2).^2/2));      % campo suave en malla GRUESA
xf = linspace(0,6,90); yf = linspace(0,4,90);
[Xf,Yf] = meshgrid(xf,yf);
Zl = interp2(Xg,Yg,Z,Xf,Yf,'linear');
Zc = interp2(Xg,Yg,Z,Xf,Yf,'cubic');
Zs = interp2(Xg,Yg,Z,Xf,Yf,'spline');
figure; contourf(Xf,Yf,Zl,20,'LineStyle','none'); colorbar; colormap(flipud(jet)); title('interp2 LINEAR (facetas)'); xlabel('x'); ylabel('y');
figure; contourf(Xf,Yf,Zc,20,'LineStyle','none'); colorbar; colormap(flipud(jet)); title('interp2 CUBIC (curvas)'); xlabel('x'); ylabel('y');
figure; contourf(Xf,Yf,Zs,20,'LineStyle','none'); colorbar; colormap(flipud(jet)); title('interp2 SPLINE (curvas)'); xlabel('x'); ylabel('y');
% chequeo: valor en un punto no-nodal, debe diferir entre metodos
p_lin = interp2(Xg,Yg,Z,2.7,1.3,'linear')
p_cub = interp2(Xg,Yg,Z,2.7,1.3,'cubic')
p_spl = interp2(Xg,Yg,Z,2.7,1.3,'spline')
