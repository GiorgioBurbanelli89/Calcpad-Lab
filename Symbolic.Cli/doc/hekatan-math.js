/* hekatan-math.js — API JS de símbolos matemáticos para Calcpad (HTML+CSS puro, clases nativas).
   El .cpd solo escribe notacion tipo LaTeX en comentarios '; esta libreria la renderiza.
   No usa MathJax/KaTeX/MathML: reusa las clases del template (.dvr .nary .dvc .dvl .o0 .r .matrix). */
(function () {
  "use strict";

  function readBrace(s, i) {            // s[i]==='{' -> [contenido, indiceSiguiente]
    var depth = 0;
    for (var j = i; j < s.length; j++) {
      if (s[j] === '{') depth++;
      else if (s[j] === '}') { depth--; if (depth === 0) return [s.slice(i + 1, j), j + 1]; }
    }
    return [s.slice(i + 1), s.length];
  }

  function bigop(sym, lo, hi, body) {
    return '<span class="dvr"><small>' + render(hi) + '</small><span class="nary">' + sym +
           '</span><small>' + render(lo) + '</small></span>' + render(body);
  }

  function matrix(raw) {
    var rows = raw.split(';');          // filas con ; (Calcpad colapsa el \\ de LaTeX a un solo \)
    var h = '<span class="matrix">';
    for (var r = 0; r < rows.length; r++) {
      h += '<span class="tr"><span class="td"></span>';
      var cells = rows[r].split('&');
      for (var c = 0; c < cells.length; c++) h += '<span class="td">' + render(cells[c].trim()) + '</span>';
      h += '<span class="td"></span></span>';
    }
    return h + '</span>';
  }

  var GREEK = {
    alpha:'α',beta:'β',gamma:'γ',delta:'δ',epsilon:'ε',zeta:'ζ',eta:'η',theta:'θ',
    lambda:'λ',mu:'μ',nu:'ν',xi:'ξ',pi:'π',rho:'ρ',sigma:'σ',tau:'τ',phi:'φ',chi:'χ',psi:'ψ',omega:'ω',
    Delta:'Δ',Sigma:'Σ',Omega:'Ω',Phi:'Φ',Psi:'Ψ',Gamma:'Γ',Lambda:'Λ',Pi:'Π',
    partial:'∂',nabla:'∇',pm:'±',mp:'∓',cdot:'·',times:'×',div:'÷',
    leq:'≤',geq:'≥',neq:'≠',approx:'≈',infty:'∞',in:'∈',to:'→',Rightarrow:'⇒',propto:'∝',
    forall:'∀',exists:'∃',cup:'∪',cap:'∩',subset:'⊂'
  };

  var FUNCS = {sin:1,cos:1,tan:1,cot:1,sec:1,csc:1,sinh:1,cosh:1,tanh:1,
    arcsin:1,arccos:1,arctan:1,ln:1,log:1,exp:1,lim:1,max:1,min:1,det:1,dim:1,gcd:1,deg:1,arg:1,mod:1};

  function expand(cmd, opt, a) {
    switch (cmd) {
      case 'frac': return '<span class="dvc">' + render(a[0]) + '<span class="dvl"></span>' + render(a[1]) + '</span>';
      case 'pard': return '<span class="dvc">∂' + render(a[0]) + '<span class="dvl"></span>∂' + render(a[1]) + '</span>';
      case 'sqrt':
        if (opt !== null) return '&hairsp;<sup class="nth">' + render(opt) + '</sup>&hairsp;&hairsp;<span class="o0"><span class="r">√</span>&hairsp;' + render(a[0]) + '</span>';
        return '&ensp;&hairsp;&hairsp;<span class="o0"><span class="r">√</span>&hairsp;' + render(a[0]) + '</span>';
      case 'int':  return bigop('<em>∫</em>', a[0], a[1], a[2] || '');
      case 'iint': return bigop('<em>∫</em>', a[0], a[1], '') + bigop('<em>∫</em>', a[0], a[1], a[2] || '');
      case 'oint': return bigop('<em>∮</em>', a[0], a[1], a[2] || '');
      case 'sum':  return bigop('∑', a[0], a[1], a[2] || '');
      case 'prod': return bigop('∏', a[0], a[1], a[2] || '');
      case 'abs':  return '<b class="b0">|</b>&hairsp;' + render(a[0]) + '&hairsp;<b class="b0">|</b>';
      case 'fn':   return '<b>' + a[0] + '</b>(' + render(a[1]) + ')';
      case 'pow':  return render(a[0]) + '<sup>' + render(a[1]) + '</sup>';
      case 'sub':  return render(a[0]) + '<sub>' + render(a[1]) + '</sub>';
      case 'T':    return render(a[0]) + '<sup>T</sup>';
      case 'v':    return '<var>' + a[0] + '</var>';
      case 'mat':  return matrix(a[0]);
      case 'vec':  return matrix(a[0]);
      default:
        if (FUNCS[cmd]) return '<b>' + cmd + '</b>';
        return GREEK[cmd] !== undefined ? GREEK[cmd] : '\\' + cmd;
    }
  }

  function render(s) {
    var out = '', i = 0;
    while (i < s.length) {
      var ch = s[i];
      if (ch === '\\') {
        var m = /^\\([a-zA-Z]+)/.exec(s.slice(i));
        if (m) {
          var cmd = m[1]; i += m[0].length;
          var opt = null;
          if (s[i] === '[') { var k = s.indexOf(']', i); opt = s.slice(i + 1, k); i = k + 1; }
          var args = [];
          while (s[i] === '{') { var rr = readBrace(s, i); args.push(rr[0]); i = rr[1]; }
          out += expand(cmd, opt, args);
          continue;
        }
      }
      if (ch === '^' || ch === '_') {                  // superindice / subindice
        var tag = ch === '^' ? 'sup' : 'sub';
        i++;
        var content;
        if (s[i] === '{') { var rb = readBrace(s, i); content = rb[0]; i = rb[1]; }
        else { content = s[i] || ''; i++; }
        out += '<' + tag + '>' + render(content) + '</' + tag + '>';
        continue;
      }
      out += ch; i++;
    }
    return out;
  }

  function inSkippable(node) {
    for (var p = node.parentNode; p; p = p.parentNode)
      if (p.tagName === 'SCRIPT' || p.tagName === 'STYLE' || p.tagName === 'INPUT') return true;
    return false;
  }

  function processAll() {
    var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, null, false);
    var nodes = [], n;
    while (n = walker.nextNode())
      if (/[\\^_]/.test(n.nodeValue) && !inSkippable(n)) nodes.push(n);
    for (var k = 0; k < nodes.length; k++) {
      var t = nodes[k], html = render(t.nodeValue);
      if (html !== t.nodeValue) {
        var span = document.createElement('span');
        span.innerHTML = html;
        t.parentNode.replaceChild(span, t);
      }
    }
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', processAll);
  else processAll();
  window.HkMath = { render: render };            // por si se quiere llamar a mano
  window.MathSym = window.HkMath;                // alias de compatibilidad (API anterior)
})();
