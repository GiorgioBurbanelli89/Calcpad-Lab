try
  eval(fileread('plate_pinned_uniform_load.m'));
  hs=findobj('Type','figure');
  for k=1:numel(hs)
    set(hs(k),'Visible','off','Position',[100 100 560 420],'PaperPositionMode','auto','Color','w');
    print(hs(k),'-dpng','-r96',sprintf('plate_pinned_uniform_load_ml%d.png',numel(hs)-k+1));
  end
  fprintf('ML figs %d\n', numel(hs));
catch e; disp(e.message); end
