// Export the user-provided Figma Make component as an exact local SVG asset.
// Usage: node scripts/export-dashboard-pulse.cjs <path-to-PulseArt.tsx>
const fs = require('node:fs');
const path = require('node:path');
const { transformSync } = require('esbuild');
const code = transformSync(fs.readFileSync(process.argv[2], 'utf8'), {
  loader: 'tsx', format: 'cjs', jsxFactory: 'h', jsxFragment: 'fragment'
}).code;
const h = (tag, props, ...children) => ({ tag, props: props || {}, children: children.flat(Infinity) });
const hooks = { useRef: () => ({ current: null }), useEffect: () => {}, useState: value => [value, () => {}] };
const moduleObject = { exports: {} };
new Function('require', 'module', 'exports', 'h', 'fragment', code)(
  name => name === 'react' ? hooks : {}, moduleObject, moduleObject.exports, h, 'fragment');
const tree = moduleObject.exports.default({ active: 'followup', onSelect: () => {} });
const svg = tree.children.find(child => child?.tag === 'svg');
svg.props = { ...svg.props, xmlns: 'http://www.w3.org/2000/svg', width: 540, height: 330 };
const escape = value => String(value).replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');
const dash = value => value.replace(/[A-Z]/g, c => '-' + c.toLowerCase());
const svgAttributes = new Set(['viewBox', 'preserveAspectRatio', 'stdDeviation', 'gradientUnits', 'gradientTransform', 'filterUnits', 'primitiveUnits']);
function serialize(node) {
  if (node == null || node === false) return '';
  if (typeof node !== 'object') return escape(node);
  const attrs = Object.entries(node.props).filter(([key, value]) => !/^on/.test(key) && key !== 'ref'
    && key !== 'key' && value != null && typeof value !== 'function').map(([key, value]) => {
      if (key === 'style') value = Object.entries(value).map(([k, v]) => `${dash(k)}:${v}`).join(';');
      return `${key === 'className' ? 'class' : key === 'tabIndex' ? 'tabindex' : svgAttributes.has(key) ? key : dash(key)}="${escape(value)}"`;
    }).join(' ');
  return `<${node.tag} ${attrs}>${node.children.map(serialize).join('')}</${node.tag}>`;
}
const destination = path.resolve(__dirname, '../src/assets/dashboard/school-pulse.svg');
fs.mkdirSync(path.dirname(destination), { recursive: true });
fs.writeFileSync(destination, serialize(svg));
console.log('Exported Figma School Pulse artwork: ' + destination);
