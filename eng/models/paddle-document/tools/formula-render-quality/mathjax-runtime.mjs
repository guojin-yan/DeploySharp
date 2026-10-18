let runtimePromise;

export function initializeMathJax() {
  if (!runtimePromise) {
    runtimePromise = (async () => {
      globalThis.MathJax = {
        loader: {
          paths: {mathjax: '@mathjax/src/bundle'},
          load: ['adaptors/liteDOM'],
          require: (file) => import(file)
        },
        svg: {fontCache: 'local'}
      };
      await import('@mathjax/src/bundle/tex-svg.js');
      await MathJax.startup.promise;
      return {
        adaptor: MathJax.startup.adaptor,
        toMathMl: (formula) => MathJax.tex2mmlPromise(String(formula), {display: true}),
        done: () => MathJax.done()
      };
    })();
  }
  return runtimePromise;
}
