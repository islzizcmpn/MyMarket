/* Scroll reveals (Layer 3).

   Progressive enhancement, strictly in that order. Nothing in app.css's reveal block applies until
   this file puts data-reveal="on" on <html>, and it only does that when IntersectionObserver exists
   and the user has not asked for reduced motion. So the failure modes all land on "visible":

     - file fails to load          -> attribute never set -> content renders normally
     - IntersectionObserver absent -> early return        -> content renders normally
     - prefers-reduced-motion      -> early return        -> content renders normally
     - anything throws later       -> disarm()            -> content becomes visible immediately

   Markup opts in with data-reveal="block" (the element itself rises) or data-reveal="stagger" (the
   element stays put and its direct children cascade). Nothing else is touched. */
(function () {
    'use strict';

    var ROOT = document.documentElement;

    /* Both must match app.css: DURATION is --reveal-dur, and STEP is the per-child delay. The
       delays are applied inline here rather than as nth-child rules in CSS so that a group of any
       length cascades without the stylesheet having to enumerate positions. */
    var DURATION = 620;
    var STEP = 70;
    var MAX_STEPS = 10;

    var SELECTOR = '[data-reveal="block"], [data-reveal="stagger"]';

    var media = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)');
    if (!('IntersectionObserver' in window) || (media && media.matches)) {
        // Still expose the API: MainLayout calls scan() unconditionally after each render.
        window.pcmarketReveal = { scan: function () { } };
        return;
    }

    ROOT.setAttribute('data-reveal', 'on');

    var seen = new WeakSet();
    var observer;

    /** Give up on the whole effect. Called on any unexpected error — a page with no animation is a
        great deal better than a page with invisible content. */
    function disarm() {
        try { if (observer) { observer.disconnect(); } } catch (e) { /* already gone */ }
        ROOT.removeAttribute('data-reveal');
    }

    /** Strip every hook once the entrance has played, leaving the element in its pristine state.
        This is not just tidiness: .reveal-in sets `transform: none`, which would otherwise keep
        overriding Layer 2's hover lift on every product card for the life of the page. */
    function settle(el, delay) {
        window.setTimeout(function () {
            el.classList.remove('reveal-in');
            el.classList.add('reveal-done');
            el.style.transitionDelay = '';
        }, delay + DURATION + 60);
    }

    function reveal(container) {
        var stagger = container.getAttribute('data-reveal') === 'stagger';
        var targets = stagger ? Array.prototype.slice.call(container.children) : [container];
        var last = 0;

        targets.forEach(function (el, i) {
            var delay = Math.min(i, MAX_STEPS) * STEP;
            last = Math.max(last, delay);
            if (delay) { el.style.transitionDelay = delay + 'ms'; }
            el.classList.add('reveal-in');
            settle(el, delay);
        });

        /* Dropped only after the cascade has finished — removing it earlier would tear the hidden
           state out from under an element mid-transition. Together with .reveal-done this gives two
           independent reasons the content can never be hidden again, so a later Blazor re-render
           that rewrote either the class or the attribute still cannot bring the curtain back down. */
        window.setTimeout(function () {
            container.removeAttribute('data-reveal');
        }, last + DURATION + 120);
    }

    observer = new IntersectionObserver(function (entries) {
        try {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) { return; }
                observer.unobserve(entry.target);
                reveal(entry.target);
            });
        } catch (e) {
            disarm();
        }
    }, {
        /* Starts the motion just before the element is fully on screen, so it reads as part of the
           scroll rather than as something that begins after it. Anything already on screen at load
           intersects immediately and plays its entrance once, which is what we want. */
        rootMargin: '0px 0px -10% 0px',
        threshold: 0.01
    });

    /** Idempotent: safe to call after every Blazor render. The WeakSet is the authority on what has
        already been handled, so an element is never observed — or revealed — twice. */
    function scan() {
        try {
            var nodes = document.querySelectorAll(SELECTOR);
            for (var i = 0; i < nodes.length; i++) {
                if (seen.has(nodes[i])) { continue; }
                seen.add(nodes[i]);
                observer.observe(nodes[i]);
            }
        } catch (e) {
            disarm();
        }
    }

    window.pcmarketReveal = { scan: scan };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', scan);
    } else {
        scan();
    }
})();
