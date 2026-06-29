namespace Company.Orchestrator.Infrastructure.BrowserPicker;

internal static class BrowserPickerCandidateScript
{
    /// <summary>
    /// Evaluated in the browser. Requires [data-alterone-pick-target="1"] on the clicked element.
    /// Returns raw candidate strings (validation happens server-side via Playwright locators).
    /// </summary>
    public const string Evaluate = """
        () => {
          const PICK_ATTR = 'data-alterone-pick-target';
          const ORIGINAL_ATTR = 'data-alterone-pick-original';
          const FORM_TAGS = new Set(['input', 'textarea', 'select']);
          const CLICKABLE = new Set(['a', 'button']);

          function cssEscape(value) {
            if (window.CSS && typeof window.CSS.escape === 'function') {
              return window.CSS.escape(value);
            }
            return String(value).replace(/[^a-zA-Z0-9_-]/g, '\\$&');
          }

          function escapeAttr(value) {
            return String(value).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
          }

          function escapeHasText(value) {
            return String(value).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
          }

          function isStableId(id) {
            if (!id || typeof id !== 'string') return false;
            if (/^\d/.test(id)) return false;
            const trimmed = id.trim();
            if (trimmed.length < 2) return false;
            if (/^[a-z][a-z0-9]*([_-][a-z0-9]+)+$/i.test(trimmed)) return true;
            if (/^[a-z][a-z0-9_-]{1,48}$/.test(trimmed) && (trimmed.includes('-') || trimmed.includes('_'))) return true;
            if (/^[a-z][a-z0-9]{2,31}$/.test(trimmed)) return true;
            if (/^[a-zA-Z0-9]+$/.test(trimmed) && !trimmed.includes('-') && !trimmed.includes('_')) {
              const hasUpper = /[A-Z]/.test(trimmed);
              const hasLower = /[a-z]/.test(trimmed);
              const hasDigit = /[0-9]/.test(trimmed);
              if (hasUpper && hasLower) return false;
              if (hasDigit && (hasUpper || hasLower) && trimmed.length >= 5) return false;
              if (trimmed.length >= 10 && /[A-Za-z]/.test(trimmed) && hasDigit) return false;
            }
            if (/^[a-f0-9-]{8,}$/i.test(trimmed)) return false;
            return /^[a-zA-Z][\w-]{1,47}$/.test(trimmed);
          }

          function attrSelector(tag, attr, value) {
            return tag + '[' + attr + '="' + escapeAttr(value) + '"]';
          }

          function visibleText(el) {
            const t = (el.innerText || el.textContent || el.value || el.getAttribute('value') || '').trim();
            return t.replace(/\s+/g, ' ');
          }

          function elementMetadata(el) {
            const tag = el.tagName.toLowerCase();
            return {
              tagName: tag,
              text: visibleText(el).slice(0, 200),
              id: el.id || '',
              name: el.getAttribute('name') || (FORM_TAGS.has(tag) ? el.name : '') || '',
              ariaLabel: el.getAttribute('aria-label') || '',
              href: el.getAttribute('href') || '',
            };
          }

          function isClickable(el, tag) {
            if (CLICKABLE.has(tag)) return true;
            const role = (el.getAttribute('role') || '').toLowerCase();
            if (role === 'button' || role === 'link') return true;
            if (tag === 'input') {
              const type = (el.getAttribute('type') || 'text').toLowerCase();
              return type === 'button' || type === 'submit' || type === 'reset';
            }
            return false;
          }

          function textTag(el, tag) {
            if (CLICKABLE.has(tag)) return tag;
            const role = (el.getAttribute('role') || '').toLowerCase();
            if (role === 'button') return 'button';
            if (role === 'link') return 'a';
            if (tag === 'input') {
              const type = (el.getAttribute('type') || '').toLowerCase();
              if (type === 'button' || type === 'submit') return 'input';
            }
            return tag;
          }

          function hrefKey(href) {
            if (!href) return null;
            try {
              const u = new URL(href, window.location.href);
              const hay = (u.pathname + u.search).toLowerCase();
              const keys = ['login', 'giris', 'signin', 'sign-in', 'register', 'kayit', 'kayit-ol', 'signup', 'cart', 'sepet', 'checkout', 'odeme'];
              for (const k of keys) {
                if (hay.includes(k)) return k;
              }
              const seg = u.pathname.split('/').filter(Boolean).pop();
              if (seg && seg.length >= 3 && seg.length <= 40 && /^[a-z0-9_-]+$/i.test(seg)) return seg;
            } catch { /* ignore */ }
            return null;
          }

          function findStableParent(el) {
            let p = el.parentElement;
            while (p && p !== document.body) {
              if (p.id && isStableId(p.id)) return p;
              p = p.parentElement;
            }
            return null;
          }

          function nthPath(el) {
            const parts = [];
            let current = el;
            while (current && current.nodeType === 1) {
              const currentTag = current.tagName.toLowerCase();
              if (currentTag === 'html' || currentTag === 'body') break;
              let nth = 1;
              let sibling = current;
              while ((sibling = sibling.previousElementSibling) != null) {
                if (sibling.tagName.toLowerCase() === currentTag) nth++;
              }
              parts.unshift(currentTag + ':nth-of-type(' + nth + ')');
              current = current.parentElement;
              if (!current) break;
              const parentTag = current.tagName.toLowerCase();
              if (parentTag === 'html' || parentTag === 'body') break;
              if (current.id && isStableId(current.id)) {
                return '#' + cssEscape(current.id) + ' > ' + parts.join(' > ');
              }
            }
            return parts.join(' > ');
          }

          function addCandidate(list, seen, selector, strategy, confidence, reason, type) {
            if (!selector || seen.has(selector)) return;
            seen.add(selector);
            list.push({ selector, strategy, confidence, reason, type: type || 'css' });
          }

          function escapeXPathLit(value) {
            const s = String(value);
            if (!s.includes("'")) return "'" + s + "'";
            if (!s.includes('"')) return '"' + s + '"';
            const parts = s.split("'");
            return "concat('" + parts.join("',\"'\",'") + "')";
          }

          const CURRENCY_CODES = new Set([
            'USD', 'EUR', 'GBP', 'CHF', 'JPY', 'AUD', 'CAD', 'SEK', 'NOK', 'DKK',
            'SAR', 'AED', 'CNY', 'RUB', 'KRW', 'TRY', 'XAU', 'XAG', 'PLN', 'HUF',
          ]);

          function detectRowCurrencyCode(row) {
            const cells = row.querySelectorAll('td, th');
            for (const cell of cells) {
              const t = visibleText(cell).trim();
              const upper = t.toUpperCase();
              if (CURRENCY_CODES.has(upper)) return upper;
              const m = t.match(/\b([A-Z]{3})\b/);
              if (m && CURRENCY_CODES.has(m[1])) return m[1];
            }
            return null;
          }

          function domChildIndex(cell) {
            const row = cell.parentElement;
            if (!row) return 1;
            let idx = 0;
            for (const child of row.children) {
              idx++;
              if (child === cell) return idx;
            }
            return 1;
          }

          function escapeXPathAttr(value) {
            return String(value).replace(/"/g, '&quot;');
          }

          function findXPathScopePrefix(el) {
            const table = el.closest('table');
            if (!table) return null;

            let current = table;
            while (current && current !== document.documentElement) {
              if (current.id && isStableId(current.id)) {
                return {
                  prefix: "//*[@id='" + escapeXPathAttr(current.id) + "']",
                  label: '#' + current.id,
                };
              }
              for (const attr of ['data-testid', 'data-test', 'data-cy']) {
                const val = current.getAttribute(attr);
                if (val) {
                  return {
                    prefix: "//*[@" + attr + "='" + escapeXPathAttr(val) + "']",
                    label: '[' + attr + '=' + val + ']',
                  };
                }
              }
              current = current.parentElement;
            }

            if (table.id && isStableId(table.id)) {
              return {
                prefix: "//table[@id='" + escapeXPathAttr(table.id) + "']",
                label: 'table#' + table.id,
              };
            }

            const tables = document.querySelectorAll('table');
            for (let i = 0; i < tables.length; i++) {
              if (tables[i] === table) {
                return { prefix: '(//table)[' + (i + 1) + ']', label: 'table[' + (i + 1) + ']' };
              }
            }

            return null;
          }

          function findCssScopePrefix(el) {
            const table = el.closest('table');
            if (!table) return null;

            let current = table;
            while (current && current !== document.documentElement) {
              if (current.id && isStableId(current.id)) {
                return '#' + cssEscape(current.id);
              }
              for (const attr of ['data-testid', 'data-test', 'data-cy']) {
                const val = current.getAttribute(attr);
                if (val) return '[' + attr + '="' + escapeAttr(val) + '"]';
              }
              current = current.parentElement;
            }

            if (table.id && isStableId(table.id)) {
              return '#' + cssEscape(table.id);
            }

            return null;
          }

          function addTableHealingCandidates(el, list, seen) {
            const tag = el.tagName.toLowerCase();
            if (tag !== 'td' && tag !== 'th') return;
            const row = el.closest('tr');
            if (!row || !row.closest('table')) return;

            const cellIndex = domChildIndex(el);
            const currency = detectRowCurrencyCode(row);
            const xpathScope = findXPathScopePrefix(el);
            const cssScope = findCssScopePrefix(el);
            const scopeNote = xpathScope ? ' scoped to ' + xpathScope.label : '';

            if (currency) {
              const rowExpr = '//tr[td[contains(normalize-space(.),' + escapeXPathLit(currency) + ')]]/' + tag + '[' + cellIndex + ']';
              const xpathSel = 'xpath=' + (xpathScope ? xpathScope.prefix : '') + rowExpr;
              addCandidate(list, seen, xpathSel, 'table-row-xpath', 'low',
                'Row-relative XPath by currency code ' + currency + ' (column ' + cellIndex + ')' + scopeNote,
                'table-relative');

              const cssBase = 'tr:has(td:has-text("' + escapeHasText(currency) + '")) ' + tag + ':nth-child(' + cellIndex + ')';
              const cssSel = cssScope ? cssScope + ' ' + cssBase : cssBase;
              addCandidate(list, seen, cssSel, 'table-row-css', 'low',
                'Row-relative CSS by currency code ' + currency + ' (column ' + cellIndex + ')' + scopeNote,
                'table-relative');
            }

            const firstCell = row.querySelector('td, th');
            if (firstCell) {
              const anchor = visibleText(firstCell).trim();
              if (anchor && anchor.length >= 2 && anchor.length <= 40 && anchor.toUpperCase() !== currency) {
                const rowExpr = '//tr[td[contains(normalize-space(.),' + escapeXPathLit(anchor) + ')]]/' + tag + '[' + cellIndex + ']';
                const xpathAnchor = 'xpath=' + (xpathScope ? xpathScope.prefix : '') + rowExpr;
                addCandidate(list, seen, xpathAnchor, 'table-row-text-xpath', 'low',
                  'Row-relative XPath by first-column text (column ' + cellIndex + ')' + scopeNote,
                  'table-relative');
              }
            }
          }

          function addXPathTextCandidates(el, list, seen) {
            const tag = el.tagName.toLowerCase();
            const text = visibleText(el);
            if (!text || text.length > 80) return;
            const snippet = text.slice(0, 60);
            const xpathSel = 'xpath=//' + tag + '[contains(normalize-space(.),' + escapeXPathLit(snippet) + ')]';
            addCandidate(list, seen, xpathSel, 'text-xpath', 'medium',
              'XPath contains visible element text', 'xpath');
          }

          const el = document.querySelector('[' + PICK_ATTR + '="1"]');
          if (!el) return null;

          const originalEl = document.querySelector('[' + ORIGINAL_ATTR + '="1"]') || el;
          const tag = el.tagName.toLowerCase();
          const text = visibleText(el);
          const id = el.id || '';
          const name = el.getAttribute('name') || (FORM_TAGS.has(tag) ? el.name : '') || '';
          const ariaLabel = el.getAttribute('aria-label') || '';
          const href = el.getAttribute('href') || '';
          const candidates = [];
          const seen = new Set();

          for (const attr of ['data-testid', 'data-test', 'data-cy']) {
            const val = el.getAttribute(attr);
            if (val) {
              addCandidate(candidates, seen, attrSelector(tag, attr, val), 'data', 'high',
                'Unique test attribute (' + attr + ')', 'attribute');
            }
          }

          if (id && isStableId(id)) {
            addCandidate(candidates, seen, '#' + cssEscape(id), 'id', 'high', 'Stable element id', 'attribute');
          }

          if (name && FORM_TAGS.has(tag)) {
            addCandidate(candidates, seen, attrSelector(tag, 'name', name), 'name', 'high',
              'Form control name attribute', 'attribute');
          } else if (name) {
            addCandidate(candidates, seen, attrSelector(tag, 'name', name), 'name', 'high',
              'Name attribute', 'attribute');
          }

          if (ariaLabel) {
            addCandidate(candidates, seen, attrSelector(tag, 'aria-label', ariaLabel), 'aria', 'medium',
              'Accessible aria-label', 'attribute');
          }

          const role = el.getAttribute('role');
          if (role) {
            addCandidate(candidates, seen, tag + '[role="' + escapeAttr(role) + '"]', 'role', 'medium',
              'Explicit ARIA role', 'attribute');
          }

          if (tag === 'a' && href) {
            const key = hrefKey(href);
            if (key) {
              addCandidate(candidates, seen, 'a[href*="' + escapeAttr(key) + '"]', 'href', 'medium',
                'Link href contains "' + key + '"', 'css');
            }
          }

          if (isClickable(el, tag) && text && text.length <= 50) {
            const tTag = textTag(el, tag);
            addCandidate(candidates, seen, tTag + ':has-text("' + escapeHasText(text) + '")', 'text', 'medium',
              'Visible text on clickable element', 'text');
          }

          const parent = findStableParent(el);
          if (parent && isClickable(el, tag) && text && text.length <= 50) {
            const tTag = textTag(el, tag);
            addCandidate(candidates, seen,
              '#' + cssEscape(parent.id) + ' ' + tTag + ':has-text("' + escapeHasText(text) + '")',
              'parent-text', 'medium',
              'Stable parent id scoped to visible text', 'text');
          }

          if (el.className && typeof el.className === 'string') {
            const classes = el.className.trim().split(/\s+/).filter(Boolean);
            for (const cls of classes) {
              if (/^(ng-|css-|jsx-|sc-|_|Mui|ember)/.test(cls)) continue;
              if (/^[a-z0-9]{6,}$/i.test(cls) && !cls.includes('-')) continue;
              addCandidate(candidates, seen, tag + '.' + cssEscape(cls), 'class', 'low',
                'Element CSS class', 'attribute');
            }
          }

          addTableHealingCandidates(el, candidates, seen);
          addXPathTextCandidates(el, candidates, seen);

          const path = nthPath(el);
          if (path) {
            addCandidate(candidates, seen, path, 'path', 'low',
              'DOM nth-of-type path (fragile fallback)', 'dom-path');
          }

          const resolvedMeta = elementMetadata(el);
          const originalMeta = elementMetadata(originalEl);

          return {
            selectedElement: resolvedMeta,
            originalClickedElement: originalMeta,
            resolvedClickableElement: resolvedMeta,
            rawCandidates: candidates,
          };
        }
        """;
}
