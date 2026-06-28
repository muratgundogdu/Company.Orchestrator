namespace Company.Orchestrator.Infrastructure.BrowserPicker;

/// <summary>
/// Picker UI script — marks clicked elements and triggers server-side candidate generation.
/// </summary>
internal static class BrowserPickerScript
{
    public const string Source = """
        (() => {
          if (window.__alteronePickerInstalled) return;
          window.__alteronePickerInstalled = true;

          let lastHighlighted = null;
          const PICK_ATTR = 'data-alterone-pick-target';
          const ORIGINAL_ATTR = 'data-alterone-pick-original';
          const MAX_PARENT_DEPTH = 10;

          function isClickableTarget(el, tag) {
            if (tag === 'html' || tag === 'body') return false;
            if (tag === 'button' || tag === 'a') return true;
            if (tag === 'input') {
              const type = (el.getAttribute('type') || 'text').toLowerCase();
              if (type === 'button' || type === 'submit') return true;
            }
            const role = (el.getAttribute('role') || '').toLowerCase();
            if (role === 'button' || role === 'link') return true;
            for (const attr of ['data-testid', 'data-test', 'data-cy']) {
              if (el.getAttribute(attr)) return true;
            }
            if (el.hasAttribute('onclick')) return true;
            return false;
          }

          function resolveClickableParent(el) {
            const original = el;
            let current = el;
            for (let depth = 0; depth <= MAX_PARENT_DEPTH && current; depth++) {
              const tag = current.tagName.toLowerCase();
              if (tag === 'html' || tag === 'body') break;
              if (isClickableTarget(current, tag)) {
                return { original, resolved: current };
              }
              if (!current.parentElement || current.parentElement === document.body) {
                const parentTag = current.parentElement ? current.parentElement.tagName.toLowerCase() : '';
                if (parentTag === 'body') break;
              }
              current = current.parentElement;
              if (!current) break;
              const nextTag = current.tagName.toLowerCase();
              if (nextTag === 'html' || nextTag === 'body') break;
            }
            return { original, resolved: original };
          }

          function highlight(el) {
            if (lastHighlighted) {
              lastHighlighted.style.outline = lastHighlighted.dataset.alteroneOutline || '';
              delete lastHighlighted.dataset.alteroneOutline;
            }
            lastHighlighted = el;
            if (el && el.style) {
              el.dataset.alteroneOutline = el.style.outline || '';
              el.style.outline = '2px solid #0d9488';
            }
          }

          document.addEventListener('mouseover', (e) => {
            if (e.target && e.target.nodeType === 1) highlight(e.target);
          }, true);

          document.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            const clicked = e.target;
            if (!clicked || clicked.nodeType !== 1) return;

            const { original, resolved } = resolveClickableParent(clicked);
            highlight(resolved);

            document.querySelectorAll('[' + PICK_ATTR + '],[' + ORIGINAL_ATTR + ']').forEach((n) => {
              n.removeAttribute(PICK_ATTR);
              n.removeAttribute(ORIGINAL_ATTR);
            });

            original.setAttribute(ORIGINAL_ATTR, '1');
            resolved.setAttribute(PICK_ATTR, '1');

            if (typeof window.alteroneReportSelector === 'function') {
              window.alteroneReportSelector('pick');
            }
          }, true);
        })();
        """;
}
